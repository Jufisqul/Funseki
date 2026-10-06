using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Generates the greybox kit: grid texture, URP Lit materials, procedural meshes and
    // PF_Kit_* prefabs. Re-runnable: existing assets are updated in place, so GUIDs and
    // references from scenes survive a regeneration.
    //
    // Module conventions: pivot at the bottom-left corner on the floor, +X along the
    // module, +Z into the room, +Y up. Walls occupy z in [0, wallThickness].
    public static class KitGenerator
    {
        public const string Root = "Assets/_Project/School/";
        public const string KitDir = Root + "Kit/";
        public const string PrefabDir = KitDir + "Prefabs/";
        public const string MeshDir = KitDir + "Meshes/";
        public const string MaterialDir = KitDir + "Materials/";
        public const string TextureDir = KitDir + "Textures/";
        public const string SettingsPath = KitDir + "KitSettings.asset";
        public const string EnvironmentLayer = "Environment";

        enum Cat { Wall, Floor, Door, Prop, Interactive }

        static KitSettings S;
        static Material[] _mats;
        static int _envLayer;

        [MenuItem("Tools/Funseki/Generate Kit")]
        public static void Generate()
        {
            foreach (var dir in new[] { PrefabDir, MeshDir, MaterialDir, TextureDir })
                Directory.CreateDirectory(dir);

            S = LoadOrCreateSettings();
            _envLayer = LayerMask.NameToLayer(EnvironmentLayer);
            if (_envLayer < 0)
            {
                Debug.LogError("[Funseki] Layer 'Environment' is missing. Add it in Tags and Layers, then run Generate Kit again.");
                return;
            }

            BuildGridTexture();
            _mats = new[]
            {
                MakeMaterial("M_Kit_Wall", S.wallColor),
                MakeMaterial("M_Kit_Floor", S.floorColor),
                MakeMaterial("M_Kit_Door", S.doorColor),
                MakeMaterial("M_Kit_Prop", S.propColor),
                MakeMaterial("M_Kit_Interactive", S.interactiveColor),
            };

            int count = 0;
            count += BuildArchitecture();
            count += BuildDoors();
            count += BuildWallsWithDoors();
            count += BuildProps();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Funseki] Kit generated: {count} prefabs in {PrefabDir}");
        }

        public static KitSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<KitSettings>(SettingsPath);
            if (settings != null) return settings;
            Directory.CreateDirectory(KitDir);
            settings = ScriptableObject.CreateInstance<KitSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        // ---------- textures and materials ----------

        static string GridPath => TextureDir + "T_Kit_Grid.png";

        static void BuildGridTexture()
        {
            int n = Mathf.Max(16, S.gridTextureSize);
            int half = Mathf.Max(1, S.gridLinePixels / 2);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var line = Color.white * (1f - S.gridLineDarkness);
            line.a = 1f;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool edge = x < half || x >= n - half || y < half || y >= n - half;
                pixels[y * n + x] = edge ? line : Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(GridPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GridPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GridPath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        static Material MakeMaterial(string name, Color color)
        {
            string path = MaterialDir + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var grid = AssetDatabase.LoadAssetAtPath<Texture2D>(GridPath);
            mat.SetTexture("_BaseMap", grid);
            mat.SetColor("_BaseColor", color);
            mat.SetTextureScale("_BaseMap", Vector2.one / Mathf.Max(0.01f, S.gridStep));
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- prefab plumbing ----------

        static Material[] Mats(params Cat[] cats)
        {
            var result = new Material[cats.Length];
            for (int i = 0; i < cats.Length; i++) result[i] = _mats[(int)cats[i]];
            return result;
        }

        static Mesh SaveMesh(string name, KitMeshBuilder b)
        {
            string path = MeshDir + "SM_Kit_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "SM_Kit_" + name };
            b.FillMesh(mesh);
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Puts the mesh, materials and box colliders on an existing GameObject.
        static void Dress(GameObject go, string meshName, KitMeshBuilder b, Material[] mats)
        {
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(meshName, b);
            var renderer = go.AddComponent<MeshRenderer>();
            var slots = new Material[b.SubMeshCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = mats[Mathf.Min(i, mats.Length - 1)];
            renderer.sharedMaterials = slots;
            foreach (var c in b.Colliders)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = c.center;
                box.size = c.size;
            }
        }

        static GameObject SavePrefab(GameObject go, bool environment)
        {
            if (environment)
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = _envLayer;
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.ReflectionProbeStatic);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + go.name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Make(string name, bool environment, Material[] mats, Action<KitMeshBuilder> build)
        {
            var b = new KitMeshBuilder();
            build(b);
            var go = new GameObject("PF_Kit_" + name);
            Dress(go, name, b, mats);
            return SavePrefab(go, environment);
        }

        static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "PF_Kit_" + name + ".prefab");

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ---------- architecture ----------

        static int BuildArchitecture()
        {
            float w = S.wallModuleWidth, h = S.ceilingHeight, t = S.wallThickness;
            var wall = Mats(Cat.Wall);
            var floor = Mats(Cat.Floor);
            int n = 0;

            Make("Wall", true, wall, b => b.Box(V(0, 0, 0), V(w, h, t))); n++;
            Make("Wall_1m", true, wall, b => b.Box(V(0, 0, 0), V(S.gridStep, h, t))); n++;

            Make("WallWindow", true, wall, b =>
            {
                float x0 = (w - S.windowWidth) * 0.5f, x1 = x0 + S.windowWidth;
                float y0 = S.windowSillHeight, y1 = y0 + S.windowHeight;
                b.Box(V(0, 0, 0), V(x0, h, t));
                b.Box(V(x1, 0, 0), V(w, h, t));
                b.Box(V(x0, 0, 0), V(x1, y0, t));
                b.Box(V(x0, y1, 0), V(x1, h, t));
            }); n++;

            Make("WallDoorway", true, wall, b => DoorwayBoxes(b, w, S.doorWidth, S.doorHeight)); n++;
            Make("WallDoorwayDouble", true, wall, b =>
                DoorwayBoxes(b, S.doubleDoorModuleWidth, S.doubleDoorWidth, S.doubleDoorHeight)); n++;

            // Outer corner: an L-shaped cap that wraps a protruding corner from outside.
            float c = S.cornerSize;
            Make("CornerOuter", true, wall, b =>
            {
                b.Box(V(-t, 0, -t), V(c, h, 0));
                b.Box(V(-t, 0, 0), V(0, h, c));
            }); n++;

            // Inner corner: a post filling the corner where two walls meet inside a room.
            Make("CornerInner", true, wall, b => b.Box(V(0, 0, 0), V(t, h, t))); n++;

            float slab = S.slabThickness;
            Make("Floor", true, floor, b => b.Box(V(0, -slab, 0), V(w, 0, w))); n++;
            Make("Floor_1x1", true, floor, b => b.Box(V(0, -slab, 0), V(S.gridStep, 0, S.gridStep))); n++;

            float cp = S.ceilingPanelThickness;
            Make("Ceiling", true, wall, b => b.Box(V(0, h - cp, 0), V(w, h, w))); n++;
            Make("Ceiling_1x1", true, wall, b => b.Box(V(0, h - cp, 0), V(S.gridStep, h, S.gridStep))); n++;

            // One flight climbs half a floor along +Z.
            Make("StairFlight", true, floor, b =>
            {
                float rise = S.StairRise, run = S.stairTread;
                for (int i = 0; i < S.stairStepsPerFlight; i++)
                    b.Box(V(0, 0, i * run), V(S.stairWidth, (i + 1) * rise, (i + 1) * run));
            }); n++;

            Make("Railing", true, wall, b =>
            {
                float rh = S.railingHeight, p = 0.05f;
                b.Box(V(0, 0, 0), V(p, rh, p), 0, false);
                b.Box(V(w - p, 0, 0), V(w, rh, p), 0, false);
                b.Box(V(0, rh - p, 0), V(w, rh, p), 0, false);
                b.Box(V(0, rh * 0.5f - 0.02f, 0.01f), V(w, rh * 0.5f + 0.02f, p - 0.01f), 0, false);
                b.ColliderOnly(V(0, 0, 0), V(w, rh, p));
            }); n++;

            Make("Column", true, wall, b => b.Box(V(0, 0, 0), V(S.columnSize, h, S.columnSize))); n++;

            // Stage block for the assembly hall: 2x2 m, stageHeight tall.
            Make("StageBlock", true, floor, b => b.Box(V(0, 0, 0), V(w, S.stageHeight, w))); n++;
            return n;
        }

        static void DoorwayBoxes(KitMeshBuilder b, float w, float doorW, float doorH)
        {
            float h = S.ceilingHeight, t = S.wallThickness;
            float x0 = (w - doorW) * 0.5f, x1 = x0 + doorW;
            b.Box(V(0, 0, 0), V(x0, h, t));
            b.Box(V(x1, 0, 0), V(w, h, t));
            b.Box(V(x0, doorH, 0), V(x1, h, t));
        }

        // ---------- doors ----------

        static int BuildDoors()
        {
            var mats = Mats(Cat.Door, Cat.Interactive);
            float dw = S.doorWidth, dh = S.doorHeight, dt = S.doorThickness;

            // Swing door: pivot on the hinge, leaf along +X.
            Make("Door_Swing", false, mats, b =>
            {
                b.Box(V(0.01f, 0, 0), V(dw - 0.01f, dh - 0.01f, dt));
                b.Box(V(dw - 0.18f, 0.95f, -0.04f), V(dw - 0.08f, 1.05f, dt + 0.04f), 1, false);
            });

            // Japanese sliding door: overlaps the opening by 5 cm on each side, recessed pull.
            Make("Door_Sliding", false, mats, b =>
            {
                float ov = 0.05f;
                b.Box(V(0, 0, 0), V(dw + ov * 2f, dh + ov, dt));
                b.Box(V(0.12f, 0.6f, -0.005f), V(dw * 0.6f, dh * 0.85f, 0.005f), 0, false); // glazing frame hint
                b.Box(V(dw + ov * 2f - 0.12f, 0.9f, -0.01f), V(dw + ov * 2f - 0.06f, 1.1f, dt + 0.01f), 1, false);
            });

            // Double door: two leaves as children so they can swing independently later.
            {
                float leaf = S.doubleDoorWidth * 0.5f, ddh = S.doubleDoorHeight;
                var leafMesh = new KitMeshBuilder()
                    .Box(V(0.01f, 0, 0), V(leaf - 0.005f, ddh - 0.01f, dt))
                    .Box(V(leaf - 0.15f, 0.95f, -0.04f), V(leaf - 0.07f, 1.15f, dt + 0.04f), 1, false);

                var root = new GameObject("PF_Kit_Door_Double");
                var left = new GameObject("Leaf_L");
                left.transform.SetParent(root.transform, false);
                Dress(left, "Door_DoubleLeaf", leafMesh, mats);

                var right = new GameObject("Leaf_R");
                right.transform.SetParent(root.transform, false);
                right.transform.localPosition = V(S.doubleDoorWidth, 0, dt);
                right.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                right.AddComponent<MeshFilter>().sharedMesh = left.GetComponent<MeshFilter>().sharedMesh;
                right.AddComponent<MeshRenderer>().sharedMaterials = left.GetComponent<MeshRenderer>().sharedMaterials;
                var lc = left.GetComponent<BoxCollider>();
                var rc = right.AddComponent<BoxCollider>();
                rc.center = lc.center;
                rc.size = lc.size;
                SavePrefab(root, false);
            }
            return 3;
        }

        // Walls with a door already set in the opening (nested door prefab).
        static int BuildWallsWithDoors()
        {
            float w = S.wallModuleWidth, t = S.wallThickness;
            float x0 = (w - S.doorWidth) * 0.5f;

            var go = new GameObject("PF_Kit_WallSlidingDoor");
            Dress(go, "WallDoorway", new KitMeshBuilder().Also(b => DoorwayBoxes(b, w, S.doorWidth, S.doorHeight)), Mats(Cat.Wall));
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = _envLayer;
            var door = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("Door_Sliding"));
            door.transform.SetParent(go.transform, false);
            door.transform.localPosition = V(x0 - 0.05f, 0, t);
            door.name = "Door";
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + go.name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        // ---------- props ----------

        static int BuildProps()
        {
            var prop = Mats(Cat.Prop, Cat.Interactive);
            var inter = Mats(Cat.Interactive, Cat.Prop);
            int n = 0;

            Make("Desk", false, prop, b =>
            {
                float lw = 0.04f;
                b.Box(V(0, 0.67f, 0), V(0.6f, 0.7f, 0.45f));
                b.Box(V(0.04f, 0.55f, 0.05f), V(0.56f, 0.6f, 0.4f), 0, false);
                foreach (var p in new[] { V(0, 0, 0), V(0.6f - lw, 0, 0), V(0, 0, 0.45f - lw), V(0.6f - lw, 0, 0.45f - lw) })
                    b.Box(p, p + V(lw, 0.67f, lw), 0, false);
                b.ColliderOnly(V(0, 0, 0), V(0.6f, 0.7f, 0.45f));
            }); n++;

            Make("Chair", false, prop, b =>
            {
                float lw = 0.03f;
                b.Box(V(0, 0.4f, 0), V(0.4f, 0.43f, 0.4f));
                b.Box(V(0, 0.43f, 0.37f), V(0.4f, 0.8f, 0.4f));
                foreach (var p in new[] { V(0, 0, 0), V(0.4f - lw, 0, 0), V(0, 0, 0.4f - lw), V(0.4f - lw, 0, 0.4f - lw) })
                    b.Box(p, p + V(lw, 0.4f, lw), 0, false);
                b.ColliderOnly(V(0, 0, 0), V(0.4f, 0.4f, 0.4f));
            }); n++;

            Make("TeacherDesk", false, prop, b =>
            {
                b.Box(V(0, 0.72f, 0), V(1.2f, 0.75f, 0.7f));
                b.Box(V(0, 0, 0), V(0.05f, 0.72f, 0.7f));
                b.Box(V(1.15f, 0, 0), V(1.2f, 0.72f, 0.7f));
                b.Box(V(0.05f, 0.2f, 0.6f), V(1.15f, 0.72f, 0.65f));
            }); n++;

            Make("Blackboard", false, prop, b =>
            {
                b.Box(V(0, 0.9f, 0), V(3.6f, 2.1f, 0.04f));
                b.Box(V(0, 0.88f, 0), V(3.6f, 0.9f, 0.08f));
            }); n++;

            Make("CarpetBoard", false, prop, b =>
            {
                b.Box(V(0, 0.8f, 0), V(3f, 2.4f, 0.03f));
                b.Box(V(-0.05f, 2.4f, 0), V(3.05f, 2.45f, 0.05f), 0, false);
            }); n++;

            Make("ShoeLocker", false, prop, b =>
            {
                float lw = 1f, d = 0.4f, lh = 1.8f, p = 0.02f;
                b.Box(V(0, 0, 0), V(p, lh, d), 0, false);
                b.Box(V(lw - p, 0, 0), V(lw, lh, d), 0, false);
                b.Box(V(0, 0, 0), V(lw, lh, p), 0, false); // back, against the wall
                b.Box(V(lw * 0.5f - p * 0.5f, 0, 0), V(lw * 0.5f + p * 0.5f, lh, d), 0, false);
                for (int i = 0; i <= 5; i++)
                {
                    float y = i * (lh - p) / 5f;
                    b.Box(V(0, y, 0), V(lw, y + p, d), 0, false);
                }
                b.ColliderOnly(V(0, 0, 0), V(lw, lh, d));
            }); n++;

            Make("Bench", false, prop, b =>
            {
                b.Box(V(0, 0.42f, 0), V(2f, 0.45f, 0.4f));
                b.Box(V(0.1f, 0, 0.05f), V(0.15f, 0.42f, 0.35f));
                b.Box(V(1.85f, 0, 0.05f), V(1.9f, 0.42f, 0.35f));
            }); n++;

            Make("BunkBed", false, prop, b =>
            {
                float bw = 0.9f, bl = 2f, bh = 1.8f, p = 0.06f;
                foreach (var c in new[] { V(0, 0, 0), V(bw - p, 0, 0), V(0, 0, bl - p), V(bw - p, 0, bl - p) })
                    b.Box(c, c + V(p, bh, p), 0, false);
                b.Box(V(0, 0.3f, 0), V(bw, 0.5f, bl));
                b.Box(V(0, 1.3f, 0), V(bw, 1.5f, bl));
                b.Box(V(bw - 0.03f, 1.5f, 0.6f), V(bw, 1.75f, bl), 0, false); // guard rail
                for (int i = 1; i <= 3; i++) // ladder rungs at the foot
                    b.Box(V(0.2f, 0.3f * i + 0.2f, bl - 0.03f), V(0.6f, 0.3f * i + 0.23f, bl), 0, false);
                b.ColliderOnly(V(0, 0, 0), V(bw, bh, bl));
            }); n++;

            Make("Sink", false, prop, b =>
            {
                b.Box(V(0, 0.75f, 0), V(0.6f, 0.88f, 0.45f));
                b.Box(V(0.25f, 0, 0.05f), V(0.35f, 0.75f, 0.2f));
                b.Box(V(0.27f, 0.88f, 0.02f), V(0.33f, 1.02f, 0.12f), 1, false);
            }); n++;

            Make("ToiletStall", false, prop, b =>
            {
                float sw = 1f, sd = 1.5f, sh = 2f, p = 0.03f, gap = 0.15f;
                b.Box(V(0, gap, 0), V(p, sh, sd));
                b.Box(V(sw - p, gap, 0), V(sw, sh, sd));
                b.Box(V(p, gap, sd - p), V(sw - p, sh, sd)); // stall door
                b.Box(V(0.3f, 0, 0), V(0.7f, 0.4f, 0.65f));
                b.Box(V(0.3f, 0.4f, 0), V(0.7f, 0.8f, 0.2f));
                b.Box(V(0.85f, 1f, sd - 0.06f), V(0.92f, 1.06f, sd + 0.02f), 1, false);
            }); n++;

            Make("Shower", false, prop, b =>
            {
                float s = 1f, p = 0.03f;
                b.Box(V(0, 0, 0), V(p, 2f, s));
                b.Box(V(s - p, 0, 0), V(s, 2f, s));
                b.Box(V(p, 0, 0), V(s - p, 0.05f, s), 0, false);
                b.Box(V(0.48f, 0, 0), V(0.52f, 2.1f, 0.04f), 0, false);
                b.Box(V(0.42f, 2f, 0), V(0.58f, 2.05f, 0.25f), 0, false);
                b.Box(V(0.45f, 1.05f, 0), V(0.55f, 1.15f, 0.07f), 1, false);
            }); n++;

            Make("VendingMachine", false, inter, b =>
            {
                b.Box(V(0, 0, 0), V(1f, 1.8f, 0.8f));
                b.Box(V(0.08f, 0.7f, 0.8f), V(0.68f, 1.65f, 0.82f), 1, false); // display window
                b.Box(V(0.75f, 0.9f, 0.8f), V(0.92f, 1.4f, 0.83f), 1, false);  // buttons
                b.Box(V(0.15f, 0.12f, 0.8f), V(0.85f, 0.32f, 0.82f), 1, false); // pickup slot
            }); n++;

            Make("WashingMachine", false, inter, b =>
            {
                b.Box(V(0, 0, 0), V(0.6f, 0.85f, 0.6f));
                b.Box(V(0.12f, 0.2f, 0.6f), V(0.48f, 0.56f, 0.63f), 1, false); // door
                b.Box(V(0.05f, 0.72f, 0.6f), V(0.55f, 0.8f, 0.62f), 1, false); // control strip
            }); n++;

            Make("Statue", false, prop, b =>
            {
                b.Box(V(0, 0, 0), V(1f, 1f, 1f));
                b.Box(V(0.3f, 1f, 0.35f), V(0.7f, 2.1f, 0.65f));
                b.Box(V(0.36f, 2.1f, 0.36f), V(0.64f, 2.4f, 0.64f));
                b.Box(V(0.7f, 1.8f, 0.42f), V(0.82f, 2.6f, 0.58f), 0, false); // raised arm
            }); n++;

            Make("ElevatorCabin", false, prop, b =>
            {
                float cw = 2f, cd = 2f, ch = 2.4f, p = 0.05f;
                b.Box(V(0, 0, 0), V(cw, p, cd));
                b.Box(V(0, ch - p, 0), V(cw, ch, cd));
                b.Box(V(0, p, cd - p), V(cw, ch - p, cd));
                b.Box(V(0, p, 0), V(p, ch - p, cd));
                b.Box(V(cw - p, p, 0), V(cw, ch - p, cd));
                b.Box(V(p, p, 0), V(0.4f, ch - p, p));       // front jambs, open front at z = 0
                b.Box(V(1.6f, p, 0), V(cw - p, ch - p, p));
                b.Box(V(0.4f, 2.1f, 0), V(1.6f, ch - p, p));
                b.Box(V(cw - p - 0.03f, 1f, 0.2f), V(cw - p, 1.4f, 0.45f), 1, false); // button panel
            }); n++;

            Make("TrashBin", false, prop, b =>
            {
                b.Box(V(0, 0, 0), V(0.6f, 1f, 0.6f));
                b.Box(V(-0.02f, 1f, -0.02f), V(0.62f, 1.06f, 0.62f), 0, false);
            }); n++;

            BuildMeetingSign(); n++;
            return n;
        }

        // Plate with the text "Совещание идёт", readable from +Z.
        static void BuildMeetingSign()
        {
            float pw = 0.8f, ph = 0.22f, y0 = 1.6f, d = 0.02f;
            var b = new KitMeshBuilder().Box(V(0, y0, 0), V(pw, y0 + ph, d));
            var go = new GameObject("PF_Kit_SignMeeting");
            Dress(go, "SignMeeting", b, Mats(Cat.Interactive));

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            text.transform.localPosition = V(pw * 0.5f, y0 + ph * 0.5f, d + 0.002f);
            text.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var tm = text.AddComponent<TextMesh>();
            tm.text = "Совещание идёт";
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.01f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.black;
            var mr = text.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;

            // Fit the text into the plate with a small margin.
            float width = mr.bounds.size.x;
            if (width > 0.001f)
            {
                float scale = Mathf.Min((pw - 0.08f) / width, (ph - 0.06f) / Mathf.Max(0.001f, mr.bounds.size.y));
                tm.characterSize *= scale;
            }
            SavePrefab(go, false);
        }

        static KitMeshBuilder Also(this KitMeshBuilder b, Action<KitMeshBuilder> a)
        {
            a(b);
            return b;
        }
    }
}
