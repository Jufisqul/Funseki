using System.Collections.Generic;
using System.IO;
using System.Linq;
using Funseki.Heroes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Characters > Setup Ryuta model (import, materials, prefab):
    // the new toon model of Ryuta, built in Blender by Tools/CharacterGen/ryuta_build.py (Art/Characters/Ryuta/Ryuta.fbx).
    // - FBX import: Humanoid avatar from the model's own skeleton (bones are named after HumanBodyBones, the A-pose
    //   is straightened into a T-pose for the avatar), blend shapes without normals, the model's custom normals,
    //   axis conversion baked so the root has no rotation;
    // - textures: Ryuta_Body / Ryuta_Face (sRGB, alpha kept), Ryuta_Ramp (linear, clamped, no mips);
    // - materials, created once and then left to the artist: Ryuta_Body, Ryuta_Face (alpha clip, no received
    //   shadows), Ryuta_Hair on Funseki/ToonLit, Ryuta_Outline on Funseki/ToonOutline; the FBX materials are remapped;
    // - Ryuta.prefab (rebuilt every time): the model + Animator with the avatar + an "Ryuta_Outline" renderer that
    //   shares the mesh and bones and copies the blend shape weights (OutlineBlendShapeSync).
    // The playable hero still uses its old model; swapping it in is a separate step.
    public static class RyutaV3Setup
    {
        const string Dir = "Assets/Characters/Ryuta";
        const string FbxPath = Dir + "/Ryuta.fbx";
        const string TexDir = Dir + "/Textures";
        const string MatDir = Dir + "/Materials";
        const string PrefabPath = Dir + "/Ryuta.prefab";
        const string Body = "Ryuta_Body", Face = "Ryuta_Face", Hair = "Ryuta_Hair", Outline = "Ryuta_Outline";

        static readonly string[] Shapes =
            { "blink_L", "blink_R", "angry", "bored", "smirk", "surprised", "mouth_A", "mouth_I", "mouth_U", "mouth_E", "mouth_O" };

        [MenuItem("Tools/Funseki/Ryuta V3/Setup Ryuta model (import, materials, prefab)")]
        public static void Setup()
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter mi)
            {
                Debug.LogError($"[RyutaV3Setup] No model at {FbxPath}: run Tools/CharacterGen/ryuta_build.py in Blender.");
                return;
            }
            SetupTexture(Body, true, 1024, false);
            SetupTexture(Face, true, 512, false);
            SetupTexture("Ryuta_Ramp", false, 256, true);
            var mats = SetupMaterials();
            if (mats == null) return;
            SetupModel(mi, mats);
            BuildPrefab(mats[Outline]);
            AssetDatabase.SaveAssets();
            Report();
        }

        // ---------------------------------------------------------------- textures and materials

        static void SetupTexture(string name, bool srgb, int maxSize, bool ramp)
        {
            string path = $"{TexDir}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
            {
                Debug.LogWarning($"[RyutaV3Setup] Missing texture {path}.");
                return;
            }
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;          // keep the colours under alpha 0 (skin mask on the body atlas)
            ti.mipmapEnabled = !ramp;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = ramp ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static Dictionary<string, Material> SetupMaterials()
        {
            var lit = Shader.Find("Funseki/ToonLit");
            var outline = Shader.Find("Funseki/ToonOutline");
            if (lit == null || outline == null)
            {
                Debug.LogError("[RyutaV3Setup] Shaders Funseki/ToonLit or Funseki/ToonOutline not found (Art/Shaders).");
                return null;
            }
            Directory.CreateDirectory(MatDir);
            Texture2D body = Tex(Body), face = Tex(Face), ramp = Tex("Ryuta_Ramp");
            var mats = new Dictionary<string, Material>
            {
                [Body] = Mat(Body, lit, body, ramp, m =>
                {
                    m.SetColor("_ShadowColor", new Color(0.62f, 0.60f, 0.80f));
                    m.SetColor("_SkinShadowColor", new Color(0.93f, 0.74f, 0.74f));
                }),
                [Face] = Mat(Face, lit, face, ramp, m =>
                {
                    m.SetColor("_ShadowColor", new Color(0.93f, 0.74f, 0.74f));
                    m.SetFloat("_ShadowReceive", 0f);    // no hair shadows on the face: clean anime face
                    m.SetFloat("_AlphaClip", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                }),
                [Hair] = Mat(Hair, lit, body, ramp, m =>
                {
                    m.SetColor("_ShadowColor", new Color(0.55f, 0.56f, 0.78f));
                    m.SetFloat("_RimStrength", 0.22f);
                }),
                [Outline] = Mat(Outline, outline, null, null, _ => { }),
            };
            return mats;
        }

        // Created once with the defaults above; later runs only re-link the shader and textures, so tweaks survive.
        static Material Mat(string name, Shader shader, Texture2D baseMap, Texture2D ramp, System.Action<Material> init)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                init(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (ramp != null) mat.SetTexture("_RampMap", ramp);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- model import and avatar

        static void SetupModel(ModelImporter mi, Dictionary<string, Material> mats)
        {
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importBlendShapes = true;
            mi.importBlendShapeNormals = ModelImporterNormals.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importAnimation = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var name in new[] { Body, Face, Hair })
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mats[name]);
            mi.animationType = ModelImporterAnimationType.Generic;   // plain import first, to read the skeleton
            mi.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            mi.humanDescription = HumanFromModel(model);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.SaveAndReimport();
        }

        static HumanDescription HumanFromModel(GameObject model)
        {
            var go = Object.Instantiate(model);
            go.name = model.name;
            try
            {
                var all = go.GetComponentsInChildren<Transform>(true);
                var bones = new Dictionary<string, Transform>();
                foreach (var t in all) bones[t.name] = t;

                // The model is in A-pose; the avatar wants arms and fingers straight out (his left is -X in Unity).
                foreach (var side in new[] { "Left", "Right" })
                {
                    var dir = side == "Left" ? Vector3.left : Vector3.right;
                    Align(bones, side + "UpperArm", side + "LowerArm", dir);
                    Align(bones, side + "LowerArm", side + "Hand", dir);
                    Align(bones, side + "Hand", side + "MiddleProximal", dir);
                    foreach (var f in new[] { "Index", "Middle", "Ring", "Little" })
                    {
                        Align(bones, side + f + "Proximal", side + f + "Intermediate", dir);
                        Align(bones, side + f + "Intermediate", side + f + "Distal", dir);
                    }
                }

                var human = new List<HumanBone>();
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    string boneName = ((HumanBodyBones)i).ToString();
                    if (!bones.ContainsKey(boneName)) continue;
                    human.Add(new HumanBone
                    {
                        humanName = HumanTrait.BoneName[i],
                        boneName = boneName,
                        limit = new HumanLimit { useDefaultValues = true },
                    });
                }
                var skeleton = all.Select(t => new SkeletonBone
                {
                    name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                }).ToArray();
                return new HumanDescription
                {
                    human = human.ToArray(),
                    skeleton = skeleton,
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
                };
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void Align(Dictionary<string, Transform> bones, string bone, string child, Vector3 dir)
        {
            if (!bones.TryGetValue(bone, out var t) || !bones.TryGetValue(child, out var c)) return;
            t.rotation = Quaternion.FromToRotation(c.position - t.position, dir) * t.rotation;
        }

        static Avatar LoadAvatar() => AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();

        // ---------------------------------------------------------------- prefab

        static void BuildPrefab(Material outlineMat)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                go.name = "Ryuta";
                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animator.avatar = LoadAvatar();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var body = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.sharedMesh.blendShapeCount > 0);
                body.updateWhenOffscreen = false;
                var outlineGo = new GameObject(Outline);
                outlineGo.transform.SetParent(body.transform.parent, false);
                outlineGo.transform.SetLocalPositionAndRotation(body.transform.localPosition, body.transform.localRotation);
                outlineGo.transform.localScale = body.transform.localScale;
                var outline = outlineGo.AddComponent<SkinnedMeshRenderer>();
                outline.sharedMesh = body.sharedMesh;
                outline.bones = body.bones;
                outline.rootBone = body.rootBone;
                outline.localBounds = body.localBounds;
                outline.quality = body.quality;
                outline.sharedMaterials = Enumerable.Repeat(outlineMat, body.sharedMesh.subMeshCount).ToArray();
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.lightProbeUsage = LightProbeUsage.Off;
                outline.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outlineGo.AddComponent<OutlineBlendShapeSync>().SetSource(body);
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- Unity-side previews

        // Renders the prefab with the real shaders into Art/Characters/Ryuta/previews/unity_*.png: A-pose, the project's
        // Humanoid Idle and Walk clips (retargeted through the new avatar) and a face close-up with blend shapes.
        // Works in the open editor (in a temporary additive scene far below the world) and in batch mode.
        [MenuItem("Tools/Funseki/Ryuta V3/Render Ryuta previews (Unity)")]
        public static void RenderPreviews()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[RyutaV3Setup] No prefab yet: run Setup Ryuta model first.");
                return;
            }
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                Debug.LogError("[RyutaV3Setup] Save or close the untitled scene first: previews open a temporary scene next to it.");
                return;
            }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var origin = new Vector3(0f, -1000f, 0f);
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "output", "characters", "Ryuta", "previews"));
            Directory.CreateDirectory(outDir);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.55f, 0.55f, 0.6f));
            var oldProbe = RenderSettings.ambientProbe;
            RenderSettings.ambientProbe = sh;
            try
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);

                var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.86f, 0.85f, 0.83f);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;

                GameObject Spawn(Vector3 offset, float yaw)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    go.transform.SetPositionAndRotation(origin + offset, Quaternion.Euler(0f, yaw, 0f));
                    return go;
                }

                var apose = Spawn(new Vector3(-1.1f, 0f, 0f), 180f);
                var idle = Spawn(new Vector3(0f, 0f, 0f), 200f);
                var walk = Spawn(new Vector3(1.1f, 0f, 0f), 160f);
                cam.fieldOfView = 28f;
                cam.transform.position = origin + new Vector3(0f, 1.05f, -5.6f);
                cam.transform.LookAt(origin + new Vector3(0f, 0.9f, 0f));
                Shoot(cam, null, 64, 64);           // warm-up: the first frame has no ambient probe / shadows yet
                var graphs = new List<PlayableGraph>();
                try
                {
                    Sample(idle, "Idle", 0.5f, graphs);
                    Sample(walk, "Walk", 0.35f, graphs);
                    foreach (var go in new[] { apose, idle, walk }) Freeze(go);
                    cam.fieldOfView = 28f;
                    cam.transform.position = origin + new Vector3(0f, 1.05f, -5.6f);
                    cam.transform.LookAt(origin + new Vector3(0f, 0.9f, 0f));
                    Shoot(cam, Path.Combine(outDir, "unity_lineup.png"), 1800, 1000);
                }
                finally
                {
                    foreach (var g in graphs) g.Destroy();
                }

                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(walk);
                Object.DestroyImmediate(apose);
                apose = Spawn(Vector3.zero, 180f);
                var renderers = apose.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var go in new[] { apose }) Freeze(go);
                cam.fieldOfView = 18f;
                cam.transform.position = origin + new Vector3(-0.35f, 1.66f, -1.05f);
                cam.transform.LookAt(origin + new Vector3(0f, 1.62f, 0f));
                Shoot(cam, Path.Combine(outDir, "unity_face.png"), 900, 900);
                foreach (var (shape, file) in new[] { ("mouth_A", "unity_face_A.png"), ("blink_L", "unity_face_blink.png") })
                {
                    foreach (var r in renderers)
                    {
                        int i = r.sharedMesh.GetBlendShapeIndex(shape);
                        for (int k = 0; k < r.sharedMesh.blendShapeCount; k++) r.SetBlendShapeWeight(k, k == i ? 100f : 0f);
                    }
                    Freeze(apose);
                    Shoot(cam, Path.Combine(outDir, file), 900, 900);
                }
                Debug.Log($"[RyutaV3Setup] Unity previews written to {outDir}");
            }
            finally
            {
                RenderSettings.ambientProbe = oldProbe;
                if (active.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Poses a Humanoid instance with a clip through a PlayableGraph (works outside Play mode and in batch mode).
        static void Sample(GameObject go, string clipFile, float time, List<PlayableGraph> graphs)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath($"Assets/ThirdParty/Models/Animation/{clipFile}.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (clip == null)
            {
                Debug.LogWarning($"[RyutaV3Setup] Clip {clipFile} not found, preview stays in A-pose.");
                return;
            }
            var animator = go.GetComponent<Animator>();
            var graph = PlayableGraph.Create("RyutaPreview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetTime(time);
            output.SetSourcePlayable(playable);
            graph.Evaluate(0f);
            graphs.Add(graph);
        }

        // Outside the player loop skinned meshes are not re-skinned before a manual Camera.Render, so the preview
        // bakes every skinned renderer (pose + blend shapes) into a plain mesh renderer next to it.
        static void Freeze(GameObject go)
        {
            foreach (var old in go.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.EndsWith("_Frozen")).ToList())
                Object.DestroyImmediate(old.gameObject);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh { name = smr.name + "_Baked" };
                smr.BakeMesh(mesh, true);
                var frozen = new GameObject(smr.name + "_Frozen");
                frozen.transform.SetParent(smr.transform, false);
                frozen.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = frozen.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;
                mr.shadowCastingMode = smr.shadowCastingMode;
                smr.forceRenderingOff = true;
            }
        }

        static void Shoot(Camera cam, string path, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            var prev = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            if (path != null) File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }

        // Batch entry: Unity -batchmode -projectPath . -executeMethod Funseki.EditorTools.RyutaV3Setup.BatchSetupAndRender -quit
        public static void BatchSetupAndRender()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity");
            Setup();
            RenderPreviews();
        }

        // ---------------------------------------------------------------- check

        static void Report()
        {
            var problems = new List<string>();
            var avatar = LoadAvatar();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) problems.Add("Humanoid avatar is missing or not valid");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var body = prefab != null
                ? prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name != Outline && r.sharedMesh.blendShapeCount > 0)
                : null;
            var mesh = body != null ? body.sharedMesh : null;
            if (mesh == null)
            {
                problems.Add("prefab has no body mesh");
                Debug.LogError("[RyutaV3Setup] FAIL: " + string.Join("; ", problems));
                return;
            }
            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)mesh.GetIndexCount(s) / 3;
            var shapes = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToList();
            var missingShapes = Shapes.Except(shapes).ToList();
            if (missingShapes.Count > 0) problems.Add("missing blend shapes: " + string.Join(", ", missingShapes));
            if (mesh.subMeshCount != 3) problems.Add($"expected 3 sub-meshes (body, face, hair), got {mesh.subMeshCount}");

            var animator = prefab.GetComponent<Animator>();
            int mapped = 0;
            var missingBones = new List<string>();
            if (avatar != null && avatar.isHuman)
            {
                var desc = avatar.humanDescription;
                mapped = desc.human.Length;
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    if (!HumanTrait.RequiredBone(i)) continue;
                    if (desc.human.All(h => h.humanName != HumanTrait.BoneName[i])) missingBones.Add(HumanTrait.BoneName[i]);
                }
            }
            if (missingBones.Count > 0) problems.Add("required Humanoid bones not mapped: " + string.Join(", ", missingBones));
            if (animator == null || animator.avatar != avatar) problems.Add("prefab Animator has no avatar");

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var bounds = probe.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name != Outline && r.sharedMesh.blendShapeCount > 0).bounds;
            var rootRot = probe.transform.GetChild(0).localEulerAngles;
            var pa = probe.GetComponent<Animator>();
            var toes = pa.GetBoneTransform(HumanBodyBones.LeftToes).position;
            var foot = pa.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            var lhand = pa.GetBoneTransform(HumanBodyBones.LeftHand).position;
            var nose = probe.GetComponentsInChildren<Transform>().First(x => x.name == "Head").position;
            if (toes.z <= foot.z) problems.Add("the model faces -Z (Unity characters face +Z)");
            if (lhand.x >= 0f) problems.Add("the left hand is not on -X (facing or mirroring is off)");
            Object.DestroyImmediate(probe);
            Debug.Log($"[RyutaV3Setup] {(problems.Count == 0 ? "PASS" : "FAIL: " + string.Join("; ", problems))}\n" +
                      $"triangles {tris} in {mesh.subMeshCount} sub-meshes, vertices {mesh.vertexCount}, " +
                      $"bones {body.bones.Length}, Humanoid bones mapped {mapped}, " +
                      $"toes {toes} foot {foot} left hand {lhand}, " + $"height {bounds.max.y:0.000} m (feet at {bounds.min.y:0.000}), first child rotation {rootRot}, " +
                      $"blend shapes: {string.Join(", ", shapes)}");
        }
    }
}


