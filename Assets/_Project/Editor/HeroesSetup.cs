using System.Collections.Generic;
using System.IO;
using System.Linq;
using Funseki.Core;
using Funseki.Dialogue;
using Funseki.Heroes;
using Funseki.Inventory;
using Funseki.NPC;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PlayerSettings = Funseki.Player.PlayerSettings;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Heroes: data and scene objects of Funseki.Heroes (GDD 2.1, 5.2).
    // - Data/Heroes: HeroSettings, Hero_Ryuta / Hero_Rei / Hero_Kaito, the three Q abilities, RumorSet_Day1 with two
    //   phone dialogues. Speakers of the heroes in Data/Dialogue/Speakers, NpcSettings in Data/NPC.
    // - Art/Heroes/Models: a prefab variant per hero model (Models/Ryuto Akane, Models/Rey Anime Girl, Models/Kaito Ren)
    //   with Player.controller; materials on built-in shaders (Akane's MToon) get URP Lit copies in Art/Heroes/Materials.
    // - Art/Heroes/Portraits: portraits rendered from the models for the switch window and the dialogue window.
    // Existing assets are kept (designer edits survive). The scene part is called by PlayerSliceSetup.Build.
    public static class HeroesSetup
    {
        const string DataDir = "Assets/_Project/Data/Heroes";
        const string AbilitiesDir = DataDir + "/Abilities";
        const string RumorsDir = DataDir + "/Rumors";
        const string SpeakersDir = "Assets/_Project/Data/Dialogue/Speakers";
        const string NpcDir = "Assets/_Project/Data/NPC";
        const string ArtDir = "Assets/_Project/Art/Heroes";
        const string ModelsDir = ArtDir + "/Models";
        const string MaterialsDir = ArtDir + "/Materials";
        const string PortraitsDir = ArtDir + "/Portraits";
        const string PlaceholderArt = "Assets/_Project/Art/Placeholders/Heroes";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string MarkerPath = "Assets/_Project/Data/Inventory/Items/Item_Marker.asset";

        const string RyutaModel = "Assets/Characters/Ryuta/Ryuta_Gameplay.prefab";
        const string KaitoModel = "Assets/ThirdParty/Models/Kaito/Ren/Prefabs/Ren_BasicSetup.prefab";
        static string ReiModel => PlayerSliceSetup.GirlFbx;

        // Followers start behind the leader at the entrance, left and right.
        static readonly Vector3[] FollowerOffsets = { new(-0.9f, 0f, -1.9f), new(0.9f, 0f, -1.9f) };

        public class HeroAssets
        {
            public HeroSettings settings;
            public HeroData[] heroes; // Рюта, Рэй, Кайто: keys 1, 2, 3
            public NpcSettings npc;
        }

        [MenuItem("Tools/Funseki/Heroes/Build Slice_Day1 with the three heroes")]
        static void BuildSlice() => PlayerSliceSetup.Build();

        [MenuItem("Tools/Funseki/Heroes/Create hero data, models and portraits")]
        static void CreateData()
        {
            EnsureAssets();
            Debug.Log("[HeroesSetup] Hero data is in " + DataDir);
        }

        [MenuItem("Tools/Funseki/Heroes/Re-render portraits")]
        static void RerenderPortraits()
        {
            foreach (var h in EnsureAssets().heroes) RenderPortrait(h, true);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- data

        internal static HeroAssets EnsureAssets()
        {
            foreach (var dir in new[] { DataDir, AbilitiesDir, RumorsDir, SpeakersDir, NpcDir, ModelsDir, MaterialsDir, PortraitsDir, PlaceholderArt })
                Directory.CreateDirectory(dir);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var settings = Asset<HeroSettings>($"{DataDir}/HeroSettings.asset", s =>
            {
                s.font = font;
                s.navMeshLayers = LayerMask.GetMask("Default", "Environment");
            });

            var ryutaColor = new Color(0.55f, 0.5f, 1f);
            var reiColor = new Color(1f, 0.45f, 0.65f);
            var kaitoColor = new Color(1f, 0.62f, 0.25f);
            var spRyuta = SpeakerAsset("Speaker_Ryuta", "Рюта", ryutaColor, 1.0f);
            var spRei = SpeakerAsset("Speaker_Rei", "Рэй", reiColor, 1.25f);
            var spKaito = SpeakerAsset("Speaker_Kaito", "Кайто", kaitoColor, 0.85f);
            var spFriend = SpeakerAsset("Speaker_PhoneMika", "Мика (по телефону)", new Color(0.6f, 0.9f, 0.6f), 1.4f);

            var rumors = Asset<RumorSet>($"{DataDir}/RumorSet_Day1.asset", r => FillRumors(r, spRei, spFriend));

            var voices = Asset<VoicesAbility>($"{AbilitiesDir}/Ability_Voices.asset", a =>
            {
                a.displayName = "Голоса";
                a.description = "Подсвечивает ближайший предмет, который можно взять, и подсказывает, где он.";
                a.cooldown = 20f;
                a.cooldownLine = "Голоса пока молчат.";
            });
            var phone = Asset<PhoneAbility>($"{AbilitiesDir}/Ability_Phone.asset", a =>
            {
                a.displayName = "Телефон";
                a.description = "Раз за перемену: звонок, после которого знаешь чуть больше. Иногда приходят мемы.";
                a.cooldown = 1f;
                a.usesPerBreak = 1;
                a.outOfUsesLine = "Баланс на нуле. До следующей перемены.";
                a.rumors = rumors;
            });
            // Added after the first version: a beam over the item, a brighter light.
            if (voices.beamMaterial == null)
            {
                voices.beamMaterial = BeamMaterial();
                voices.glowIntensity = Mathf.Max(voices.glowIntensity, 8f);
                voices.glowRange = Mathf.Max(voices.glowRange, 3f);
                EditorUtility.SetDirty(voices);
            }
            var kick = Asset<KickAbility>($"{AbilitiesDir}/Ability_Kick.asset", a =>
            {
                a.displayName = "Пинок";
                a.description = "Пинает того, кто стоит прямо перед ним. Учителям это не нравится.";
                a.cooldown = 3f;
            });

            var heroes = new[]
            {
                HeroAsset("Hero_Ryuta", HeroId.Ryuta, "Тамура Рюта", "Рюта", ryutaColor, RyutaModel, spRyuta, kick),
                HeroAsset("Hero_Rei", HeroId.Rei, "Кагами Рэй", "Рэй", reiColor, ReiModel, spRei, phone),
                HeroAsset("Hero_Kaito", HeroId.Kaito, "Мидзуно Кайто", "Кайто", kaitoColor, KaitoModel, spKaito, voices),
            };
            foreach (var h in heroes) RenderPortrait(h, false);

            var npc = Asset<NpcSettings>($"{NpcDir}/NpcSettings.asset", s => s.occluderLayers = LayerMask.GetMask("Environment"));
            AssetDatabase.SaveAssets();
            return new HeroAssets { settings = settings, heroes = heroes, npc = npc };
        }

        static HeroData HeroAsset(string file, HeroId id, string displayName, string shortName, Color color, string modelPath,
            Speaker speaker, HeroAbility ability)
        {
            var data = Asset<HeroData>($"{DataDir}/{file}.asset", h =>
            {
                h.id = id;
                h.displayName = displayName;
                h.shortName = shortName;
                h.color = color;
                h.speaker = speaker;
                h.ability = ability;
            });
            if (data.prefab == null)
            {
                data.prefab = ModelVariant(file.Replace("Hero_", ""), modelPath);
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        static void FillRumors(RumorSet set, Speaker rei, Speaker mika)
        {
            var whistle = Asset<DialogueGraph>($"{RumorsDir}/Dialogue_Rumor_Whistle.asset", g => g.nodes = new List<DialogueNode>
            {
                Line("1", mika, "Рэй! Слушай. Физрук на перемене оставляет свисток на подоконнике у спортзала."),
                Line("2", rei, "И зачем мне это знать?"),
                Line("3", mika, "Ну мало ли. Без свистка он только машет руками. Как мельница.", end: true),
            });
            var roof = Asset<DialogueGraph>($"{RumorsDir}/Dialogue_Rumor_Roof.asset", g => g.nodes = new List<DialogueNode>
            {
                Line("1", mika, "Говорят, дверь на крышу вообще не заперта. Замок просто нарисован."),
                Line("2", rei, "Нарисован?"),
                Line("3", mika, "Маркером. Очень убедительно. Только никому!", end: true),
            });
            set.rumors = new List<Rumor>
            {
                new() { note = "Подсказка к шалости со свистком", dialogue = whistle, forbidFlags = new[] { "prank_whistle_done" }, heardFlag = "rumor_whistle_heard" },
                new() { note = "Секрет: дверь на крышу", dialogue = roof, heardFlag = "rumor_roof_heard" },
            };
        }

        static DialogueNode Line(string id, Speaker speaker, string text, bool end = false) => new()
        {
            id = id,
            speaker = speaker,
            text = text,
            end = end,
            conditions = new List<DialogueCondition>(),
            actions = new List<DialogueAction>(),
            choices = new List<DialogueChoice>(),
        };

        static Speaker SpeakerAsset(string file, string displayName, Color color, float pitch) =>
            Asset<Speaker>($"{SpeakersDir}/{file}.asset", s =>
            {
                s.displayName = displayName;
                s.nameColor = color;
                s.mumblePitch = pitch;
            });

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // Unlit, transparent, additive pale blue: the «Голоса» light beam.
        static Material BeamMaterial()
        {
            string path = $"{AbilitiesDir}/M_VoicesBeam.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.25f, 0.45f, 0.6f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- models

        // A prefab variant of the source model: Player.controller, no root motion, URP materials.
        static GameObject ModelVariant(string name, string sourcePath)
        {
            if (sourcePath == RyutaModel)
                return AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            string path = $"{ModelsDir}/{name}_Model.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[HeroesSetup] Model not found: {sourcePath}");
                return null;
            }

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerSliceSetup.ControllerPath);
                animator.applyRootMotion = false;
                ConvertMaterials(go, name);
                var variant = PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[HeroesSetup] {path}: variant of {sourcePath}");
                return variant;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // URP can't draw built-in pipeline shaders (Akane's VRM MToon shows magenta): swap them for URP Lit copies
        // with the same texture, color, cutout and culling.
        static void ConvertMaterials(GameObject go, string heroName)
        {
            string dir = $"{MaterialsDir}/{heroName}";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !NeedsConversion(mats[i].shader)) continue;
                    Directory.CreateDirectory(dir);
                    mats[i] = UrpCopy(mats[i], dir);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static bool NeedsConversion(Shader shader)
        {
            if (shader == null || ShaderUtil.ShaderHasError(shader)) return true; // e.g. lilToon on this URP version
            string n = shader.name;
            return n.Contains("MToon") || n == "Standard" || n == "Standard (Specular setup)" || n.StartsWith("Legacy Shaders/")
                   || n == "Hidden/InternalErrorShader";
        }

        static Material UrpCopy(Material src, string dir)
        {
            string file = string.Concat(src.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string path = $"{dir}/{file}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (src.HasProperty("_MainTex"))
            {
                m.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                m.SetTextureScale("_BaseMap", src.GetTextureScale("_MainTex"));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_MainTex"));
            }
            if (src.HasProperty("_Color")) m.SetColor("_BaseColor", src.GetColor("_Color"));
            m.SetFloat("_Smoothness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            if (src.HasProperty("_CullMode")) m.SetFloat("_Cull", src.GetFloat("_CullMode"));

            // MToon _BlendMode: 0 opaque, 1 cutout, 2 transparent, 3 transparent with depth write.
            float blend = src.HasProperty("_BlendMode") ? src.GetFloat("_BlendMode") : 0f;
            if (blend >= 0.5f && blend < 1.5f)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (blend >= 1.5f)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", blend >= 2.5f ? 1f : 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- portraits

        // Head-and-shoulders shot of the model on the hero's color, saved as a sprite. Also given to the hero's
        // Speaker when it has no portrait. Skipped when the portrait exists (unless forced).
        static void RenderPortrait(HeroData hero, bool force)
        {
            if (hero == null || hero.prefab == null || (hero.portrait != null && !force)) return;
            string path = $"{PortraitsDir}/Portrait_{hero.id}.png";

            var preview = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(hero.prefab, preview);
                var animator = go.GetComponentInChildren<Animator>();
                Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                Vector3 face = head != null ? head.position + Vector3.up * 0.05f : go.transform.position + Vector3.up * 1.5f;

                var lightGo = new GameObject("Light");
                SceneManager.MoveGameObjectToScene(lightGo, preview);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(20f, 200f, 0f);

                var camGo = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(camGo, preview);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = preview;
                cam.cameraType = CameraType.Preview;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.Lerp(hero.color, Color.black, 0.45f);
                cam.fieldOfView = 26f;
                cam.nearClipPlane = 0.05f;
                camGo.transform.position = face + go.transform.forward * 1.1f - Vector3.up * 0.08f;
                camGo.transform.LookAt(face - Vector3.up * 0.1f);

                rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;

                if (IsFlat(tex))
                {
                    Debug.LogWarning($"[HeroesSetup] Portrait of {hero.displayName} came out empty; the window shows a letter card instead.");
                    return;
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HeroesSetup] Couldn't render the portrait of {hero.displayName}: {e.Message}");
                return;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (rt != null) Object.DestroyImmediate(rt);
                if (tex != null) Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            hero.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(hero);
            if (hero.speaker is Speaker sp && sp.portrait == null)
            {
                sp.portrait = hero.portrait;
                EditorUtility.SetDirty(sp);
            }
        }

        static bool IsFlat(Texture2D tex)
        {
            var px = tex.GetPixels32();
            var first = px[0];
            foreach (var p in px)
                if (Mathf.Abs(p.r - first.r) + Mathf.Abs(p.g - first.g) + Mathf.Abs(p.b - first.b) > 12) return false;
            return true;
        }

        // ---------------------------------------------------------------- scene

        // The "Heroes" root (HeroParty + HeroNavMesh) with the three heroes under it, in the active scene.
        // Returns the hero that starts in control.
        internal static GameObject BuildHeroes(HeroAssets a, RuntimeAnimatorController controller, InputActionAsset input,
            PlayerSettings playerSettings, Vector3 spawn, out List<GameObject> heroes)
        {
            var root = new GameObject("Heroes");
            heroes = new List<GameObject>();
            GameObject leader = null;
            int follower = 0;

            foreach (var data in a.heroes)
            {
                bool isLeader = data.id == a.settings.startHero;
                Vector3 pos = isLeader ? spawn : spawn + FollowerOffsets[Mathf.Min(follower++, FollowerOffsets.Length - 1)];
                var model = data.prefab != null ? data.prefab : AssetDatabase.LoadAssetAtPath<GameObject>(ReiModel);
                var go = PlayerSliceSetup.BuildHero($"Hero_{data.id}", model, controller, input, playerSettings, pos, Quaternion.identity);
                go.transform.SetParent(root.transform, true);

                var agent = go.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                var unit = go.AddComponent<HeroUnit>();
                PlayerSliceSetup.Set(unit, "data", data);
                PlayerSliceSetup.Set(unit, "settings", a.settings);
                go.AddComponent<HeroAbilityRunner>();

                var tag = go.AddComponent<SpeakerTag>();
                tag.speaker = data.speaker as Speaker;
                tag.headHeight = go.GetComponent<CharacterController>().height;

                // Shared inventory: wheel / LMB / RMB and the item in hand work for whoever is in control.
                PlayerSliceSetup.Set(go.AddComponent<HeroItemUser>(), "actions", input);
                PlayerSliceSetup.Set(go.AddComponent<HeldItemView>(), "animator", go.GetComponentInChildren<Animator>());

                heroes.Add(go);
                if (isLeader) leader = go;
            }
            if (leader == null) leader = heroes[0];

            var party = root.AddComponent<HeroParty>();
            PlayerSliceSetup.Set(party, "settings", a.settings);
            PlayerSliceSetup.Set(party, "actions", input);
            var so = new SerializedObject(party);
            var arr = so.FindProperty("heroes");
            arr.arraySize = heroes.Count;
            for (int i = 0; i < heroes.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = heroes[i].GetComponent<HeroUnit>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PlayerSliceSetup.Set(root.AddComponent<HeroNavMesh>(), "settings", a.settings);
            return leader;
        }

        // On the entrance porch, so the Q actions can be tried at once: a classmate to kick, a teacher who watches
        // him, and an item for Кайто's «Голоса».
        internal static void BuildTestContent(HeroAssets a)
        {
            var root = new GameObject("HeroesTest");
            NpcCapsule(root.transform, "NPC_TestStudent", new Vector3(28.2f, 0f, -44.6f), 250f, new Color(0.45f, 0.75f, 1f), false, a.npc);
            NpcCapsule(root.transform, "NPC_TestTeacher", new Vector3(21f, 0f, -46.8f), 72f, new Color(0.35f, 0.3f, 0.3f), true, a.npc);

            var item = AssetDatabase.LoadAssetAtPath<ItemData>(MarkerPath);
            var pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = "Pickup_TestVoices";
            pickup.transform.SetParent(root.transform, false);
            pickup.transform.SetPositionAndRotation(new Vector3(32.2f, 0.06f, -47.5f), Quaternion.Euler(0f, 30f, 0f));
            pickup.transform.localScale = new Vector3(0.14f, 0.12f, 0.3f);
            pickup.GetComponent<Renderer>().sharedMaterial = Mat("M_HeroesTest_Item", new Color(0.95f, 0.85f, 0.2f));
            var p = pickup.AddComponent<PickupItem>();
            PlayerSliceSetup.Set(p, "item", item);
            var so = new SerializedObject(p);
            so.FindProperty("voiceHint").stringValue = "Там, у края крыльца, что-то блестит…";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Root on the ground with NpcActor, a capsule body and a "nose" showing where it faces.
        static void NpcCapsule(Transform parent, string name, Vector3 pos, float yaw, Color color, bool teacher, NpcSettings settings)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var mat = Mat(teacher ? "M_HeroesTest_Teacher" : "M_HeroesTest_Student", color);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            body.GetComponent<Renderer>().sharedMaterial = mat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.45f, 0.25f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.15f);
            nose.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            var actor = root.AddComponent<NpcActor>();
            PlayerSliceSetup.Set(actor, "settings", settings);
            var so = new SerializedObject(actor);
            so.FindProperty("isTeacher").boolValue = teacher;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{PlaceholderArt}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
