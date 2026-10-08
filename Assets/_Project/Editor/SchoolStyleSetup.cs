using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Funseki.Core;
using Funseki.School;
using Funseki.School.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Restyles the frozen School_Greybox in place: procedural textures baked from SchoolPalette,
    // URP Lit materials, and more detailed versions of a few kit meshes. Only materials and meshes
    // of existing renderers change (plus glass in the window holes); no object is moved, added to
    // the plan or rebuilt. The first Apply writes a backup, Revert puts the old look back.
    public static class SchoolStyleSetup
    {
        const string PalettePath = "Assets/_Project/Data/Visual/SchoolPalette.asset";
        const string ArtDir = "Assets/_Project/Art/School/";
        const string TexDir = ArtDir + "Textures/";
        const string MatDir = ArtDir + "Materials/";
        const string MeshDir = ArtDir + "Meshes/";
        const string BackupPath = ArtDir + "SchoolStyleBackup.json";
        const string SchoolScenePath = "Assets/_Project/School/Scenes/School_Greybox.unity";
        const string KitMeshDir = "Assets/_Project/School/Kit/Meshes/";
        const string WindowChild = "Style_Window";
        const string ShotsDir = "Docs/SchoolStyle/";
        static readonly string[] Roots = { "School", "School_Furniture", "School_Props" };

        static SchoolPalette P;
        static readonly Dictionary<string, Material> M = new();

        // ---------- menu ----------

        [MenuItem("Tools/Funseki/School Style/Apply Palette and Textures to School", priority = 0)]
        public static void Apply()
        {
            P = LoadOrCreatePalette();
            BuildTexturesAndMaterials();
            var meshes = BuildMeshes();

            var scene = OpenSchool(out bool openedHere, out bool wasDirty);
            var backup = LoadBackup();
            var counts = new Dictionary<string, int>();
            int windows = 0;

            foreach (var root in RootsOf(scene))
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.transform.name == WindowChild) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string kind = KindOf(mf.sharedMesh.name);
                if (kind == null) continue;

                var zone = ZoneOf(mr.transform);
                if (!Style(kind, zone, mr, out var mats, out var meshName)) continue;
                Mesh newMesh = meshName != null ? meshes[meshName] : null;

                Remember(backup, mr, mf);
                if (newMesh != null && mf.sharedMesh != newMesh) { mf.sharedMesh = newMesh; PrefabUtility.RecordPrefabInstancePropertyModifications(mf); }
                mr.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;

                if (kind == "WallWindow" && AddWindowGlass(mr, meshes["WindowGlass"])) windows++;
            }

            SaveBackup(backup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (openedHere || !wasDirty) EditorSceneManager.SaveScene(scene);
            else Debug.LogWarning("[SchoolStyle] School_Greybox had unsaved edits before the restyle, so it was not saved automatically. Save it (Ctrl+S) to keep both.");

            AssetDatabase.SaveAssets();
            Debug.Log($"[SchoolStyle] Restyled {counts.Values.Sum()} renderers ({string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value}"))}), glass in {windows} windows. Palette: {PalettePath}");
        }

        [MenuItem("Tools/Funseki/School Style/Rebake Textures and Materials Only", priority = 1)]
        public static void RebakeOnly()
        {
            P = LoadOrCreatePalette();
            BuildTexturesAndMaterials();
            BuildMeshes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SchoolStyle] Textures, materials and meshes rebaked from the palette. The scene already points at them.");
        }

        [MenuItem("Tools/Funseki/School Style/Revert School to Greybox Look", priority = 20)]
        public static void Revert()
        {
            if (!File.Exists(BackupPath)) { Debug.LogWarning("[SchoolStyle] No backup: the school was never restyled."); return; }
            if (!EditorUtility.DisplayDialog("Revert school style",
                    "Put the old greybox materials and meshes back on School_Greybox and remove the window glass?", "Revert", "Cancel")) return;

            var scene = OpenSchool(out bool openedHere, out bool wasDirty);
            var backup = LoadBackup();
            int restored = 0;
            foreach (var e in backup.entries)
            {
                if (!GlobalObjectId.TryParse(e.id, out var gid)) continue;
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is not MeshRenderer mr) continue;
                var mf = mr.GetComponent<MeshFilter>();
                var mesh = LoadRef<Mesh>(e.mesh);
                if (mf != null && mesh != null) { mf.sharedMesh = mesh; PrefabUtility.RecordPrefabInstancePropertyModifications(mf); }
                mr.sharedMaterials = e.mats.Select(LoadRef<Material>).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
                restored++;
            }
            int removed = 0;
            foreach (var root in RootsOf(scene))
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == WindowChild).ToList())
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
                removed++;
            }
            AssetDatabase.DeleteAsset(BackupPath);
            EditorSceneManager.MarkSceneDirty(scene);
            if (openedHere || !wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SchoolStyle] Reverted {restored} renderers, removed {removed} window glass objects.");
        }

        [MenuItem("Tools/Funseki/School Style/Capture Style Shots", priority = 40)]
        public static void CaptureShots() => Capture("shot");

        // ---------- palette ----------

        static SchoolPalette LoadOrCreatePalette()
        {
            var p = AssetDatabase.LoadAssetAtPath<SchoolPalette>(PalettePath);
            if (p != null) return p;
            Directory.CreateDirectory(Path.GetDirectoryName(PalettePath));
            p = ScriptableObject.CreateInstance<SchoolPalette>();
            AssetDatabase.CreateAsset(p, PalettePath);
            return p;
        }

        // ---------- what goes where ----------

        static string KindOf(string meshName)
        {
            if (meshName.StartsWith("SM_Kit_")) return meshName.Substring(7);
            if (meshName.StartsWith("SM_School_")) return meshName.Substring(10);
            return null;
        }

        class ZoneInfo
        {
            public string id = "";
            public float floorY = float.NaN;
        }

        static ZoneInfo ZoneOf(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (!p.name.StartsWith("Zone_")) continue;
                var info = new ZoneInfo { id = p.name.Substring(5) };
                var zone = p.GetComponent<Zone>();
                if (zone != null) info.id = zone.zoneId;
                var box = p.GetComponent<BoxCollider>();
                if (box != null) info.floorY = p.TransformPoint(box.center - box.size * 0.5f).y;
                return info;
            }
            return new ZoneInfo();
        }

        static bool IsAny(string id, params string[] prefixes) => prefixes.Any(id.StartsWith);
        static bool Outdoor(string id) => IsAny(id, "courtyard", "kitchen_yard");
        static bool Wet(string id) => IsAny(id, "wc_", "showers_", "laundry", "kitchen") && !Outdoor(id);
        static bool Gym(string id) => IsAny(id, "gym", "weights", "assembly_hall") && id != "gym_vestibule";
        static bool Female(string id) => IsAny(id, "wc_f", "showers_f");

        static string FloorMat(string id)
        {
            if (Outdoor(id)) return "Floor_Ground";
            if (IsAny(id, "stairs", "floor_3", "elevator", "utility")) return id == "elevator_hall" ? "Floor_Corridor" : "Concrete";
            if (Wet(id)) return "Floor_Wet";
            if (Gym(id)) return "Floor_Gym";
            if (IsAny(id, "principal", "staff_room")) return "Floor_Carpet";
            if (id == "" || IsAny(id, "corridor", "hall_", "passage", "assembly_foyer", "gym_vestibule", "locker_", "canteen")) return "Floor_Corridor";
            return "Floor_Wood";
        }

        static string WallMat(ZoneInfo z, Renderer r)
        {
            // Upper tiers of double-height rooms (gym, hall) would repeat the bottom band at 4 m.
            bool upper = !float.IsNaN(z.floorY) && r.bounds.min.y > z.floorY + 1f;
            if (upper) return "Wall_Plain";
            if (Outdoor(z.id)) return "Wall_Facade";
            if (Wet(z.id)) return "Wall_Tile";
            if (Gym(z.id)) return "Wall_Gym";
            return "Wall_Plaster";
        }

        // Materials (and optionally a replacement mesh) for one kit renderer.
        static bool Style(string kind, ZoneInfo z, MeshRenderer r, out Material[] mats, out string mesh)
        {
            mesh = null;
            string stall = Female(z.id) ? "Stall_F" : "Stall_M";
            string[] m;
            switch (kind)
            {
                case "Wall": case "Wall_1m": case "WallWindow": case "WallDoorway": case "WallDoorwayDouble":
                case "CornerOuter": case "CornerInner": case "Column":
                    m = new[] { WallMat(z, r) }; break;
                // The slab's underside is the ceiling of the room below, so it gets its own slot.
                case "Floor": case "Floor_1x1": m = new[] { FloorMat(z.id), "Ceiling" }; mesh = kind; break;
                case "Ceiling": case "Ceiling_1x1": m = new[] { "Ceiling" }; break;
                case "StairFlight": m = new[] { "Concrete" }; break;
                case "StageBlock": m = new[] { "Floor_Wood" }; break;
                case "Railing": m = new[] { "DarkMetal" }; break;
                case "Door_Swing": m = new[] { "Door_Swing", "Handle" }; break;
                case "Door_Sliding": m = new[] { "Door_Sliding", "Handle" }; break;
                case "Door_DoubleLeaf": m = new[] { "Door_Double", "Handle" }; break;
                case "Desk": m = new[] { "Wood", "Metal" }; mesh = "Desk"; break;
                case "Chair": m = new[] { "Wood", "Metal" }; mesh = "Chair"; break;
                case "TeacherDesk": m = new[] { "Wood", "LockerSteel", "DarkMetal" }; mesh = "TeacherDesk"; break;
                case "Blackboard": m = new[] { "Blackboard", "Wood", "Metal", "Chalk" }; mesh = "Blackboard"; break;
                case "CarpetBoard": m = new[] { "Floor_Carpet" }; break;
                case "ShoeLocker": m = new[] { "LockerSteel" }; break;
                case "Bench": m = new[] { "Wood", "Metal" }; mesh = "Bench"; break;
                case "BunkBed": m = new[] { "Fabric", "Metal", "Chalk" }; mesh = "BunkBed"; break;
                case "Sink": m = new[] { "Porcelain", "Metal" }; break;
                case "ToiletStall": m = new[] { stall, "Porcelain", "Handle" }; mesh = "ToiletStall"; break;
                case "Shower": m = new[] { stall, "Metal" }; break;
                case "VendingMachine": m = new[] { "Vending_Body", "Vending_Front" }; break;
                case "WashingMachine": m = new[] { "Porcelain", "Metal" }; break;
                case "Statue": m = new[] { "Stone" }; break;
                case "ElevatorCabin": m = new[] { "Metal", "Handle" }; break;
                case "TrashBin": m = new[] { "TrashBin", "DarkMetal" }; mesh = "TrashBin"; break;
                case "SignMeeting": m = new[] { "Chalk" }; break;
                default: mats = null; return false;
            }
            mats = m.Select(n => M[n]).ToArray();
            return true;
        }

        // ---------- textures and materials ----------

        static int Ppm => Mathf.Clamp(P.pixelsPerMeter, 16, 256);

        static void BuildTexturesAndMaterials()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(MatDir);
            M.Clear();
            float ch = CeilingHeight();
            int ppm = Ppm;
            int W2 = ppm * 2, H = Mathf.RoundToInt(ppm * ch);

            Baked("Wall_Plaster", WallTex(W2, H, ch, 0), 2f, ch);
            Baked("Wall_Tile", WallTex(W2, H, ch, 1), 2f, ch);
            Baked("Wall_Gym", WallTex(W2, H, ch, 2), 2f, ch);
            Baked("Wall_Facade", WallTex(W2, H, ch, 3), 2f, ch);
            Baked("Wall_Plain", WallTex(W2, H, ch, 4), 2f, ch);

            Baked("Floor_Corridor", CorridorTex(W2), 2f, 2f);
            Baked("Floor_Wood", PlankTex(W2, P.classroomWood, 0.125f, 11), 2f, 2f);
            Baked("Floor_Gym", PlankTex(W2, P.gymWood, 0.1f, 23), 2f, 2f);
            Baked("Floor_Wet", TileTex(W2, 2f, 0.25f, P.wetTile, P.wetTileGrout, 31), 2f, 2f);
            Baked("Floor_Carpet", CarpetTex(W2), 2f, 2f);
            Baked("Floor_Ground", GroundTex(W2), 2f, 2f);
            Baked("Concrete", ConcreteTex(W2), 2f, 2f);
            Baked("Ceiling", CeilingTex(W2), 2f, 2f);

            Baked("Door_Sliding", SlidingDoorTex(1.3f, 2.25f), 1.3f, 2.25f);
            Baked("Door_Swing", SwingDoorTex(1.2f, 2.2f), 1.2f, 2.2f);
            Baked("Door_Double", SwingDoorTex(1.2f, 2.4f), 1.2f, 2.4f);
            Baked("Blackboard", BlackboardTex(ppm), 1f, 1f);
            Baked("Vending_Front", VendingTex(1f, 1.8f), 1f, 1.8f);

            var noise = SaveTex("Noise", NoiseTex(ppm, 41, 1f));
            var grain = SaveTex("Grain", GrainTex(ppm));
            Tinted("Wood", grain, P.deskWood);
            Tinted("Metal", noise, P.metal);
            Tinted("DarkMetal", noise, P.darkMetal);
            Tinted("LockerSteel", noise, P.lockerSteel);
            Tinted("Handle", noise, P.handle);
            Tinted("Chalk", noise, P.chalk);
            Tinted("Stall_M", noise, P.stallMale);
            Tinted("Stall_F", noise, P.stallFemale);
            Tinted("Porcelain", noise, P.porcelain);
            Tinted("Fabric", noise, P.fabric);
            Tinted("Vending_Body", noise, P.vendingBody);
            Tinted("Stone", noise, P.stone);
            Tinted("TrashBin", noise, P.trashBin);
            Tinted("WindowFrame", noise, P.windowFrame);
            var glass = Tinted("WindowGlass", null, P.windowGlass);
            MakeTransparent(glass);
        }

        static float CeilingHeight()
        {
            var wall = AssetDatabase.LoadAssetAtPath<Mesh>(KitMeshDir + "SM_Kit_Wall.asset");
            if (wall != null && wall.bounds.size.y > 1f) return wall.bounds.size.y;
            var kit = AssetDatabase.LoadAssetAtPath<KitSettings>("Assets/_Project/School/Kit/KitSettings.asset");
            return kit != null ? kit.ceilingHeight : 3.2f;
        }

        static void Baked(string name, Img img, float tileW, float tileH)
        {
            var tex = SaveTex(name, img);
            var mat = Mat(name);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTextureScale("_BaseMap", new Vector2(1f / tileW, 1f / tileH));
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
        }

        static Material Tinted(string name, Texture2D tex, Color color)
        {
            var mat = Mat(name);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            return mat;
        }

        static Material Mat(string name)
        {
            string path = MatDir + "M_School_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Smoothness", P.smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            MakeOpaque(mat);
            EditorUtility.SetDirty(mat);
            M[name] = mat;
            return mat;
        }

        static void MakeOpaque(Material mat)
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = -1;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.85f);
            mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static Texture2D SaveTex(string name, Img img)
        {
            string path = TexDir + "T_School_" + name + ".png";
            var tex = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false);
            tex.SetPixels(img.P);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = P.pointFilter ? FilterMode.Point : FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- image helpers ----------

        class Img
        {
            public readonly int W, H;
            public readonly Color[] P;
            public Img(int w, int h) { W = w; H = h; P = new Color[w * h]; }
            public Color this[int x, int y] { get => P[y * W + x]; set => P[y * W + x] = value; }
        }

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFF) / 65535f;

        // Value noise that tiles over [0,1) with `period` cells per side.
        static float VNoise(float u, float v, int px, int py, int seed)
        {
            float x = u * px, y = v * py;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wx(int a) => ((a % px) + px) % px;
            int Wy(int a) => ((a % py) + py) % py;
            float a00 = Rand(Wx(x0), Wy(y0), seed), a10 = Rand(Wx(x0 + 1), Wy(y0), seed);
            float a01 = Rand(Wx(x0), Wy(y0 + 1), seed), a11 = Rand(Wx(x0 + 1), Wy(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        static float Fbm(float u, float v, int px, int py, int seed, int octaves = 3)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += VNoise(u, v, px, py, seed + i * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                px *= 2; py *= 2;
            }
            return sum / norm;
        }

        static Color Shade(Color c, float n, float amount)
        {
            float k = 1f + (n - 0.5f) * 2f * amount;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        static Color Mul(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        // Meter-based painting: (u, v) in [0,1) of the image -> meters through the tile size.
        static Img Paint(int w, int h, float tileW, float tileH, Func<float, float, float, float, Color> f)
        {
            var img = new Img(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                img[x, y] = f(u, v, u * tileW, v * tileH);
            }
            return img;
        }

        static bool Near(float value, float step, float halfWidth)
        {
            float d = Mathf.Abs(value / step - Mathf.Round(value / step)) * step;
            return d < halfWidth;
        }

        static float Line => 1.2f / Mathf.Clamp(P.pixelsPerMeter, 16, 256);

        // ---------- surfaces ----------

        // style: 0 plaster + wainscot, 1 wet tiles, 2 gym panels, 3 facade, 4 plain
        static Img WallTex(int w, int h, float ch, int style)
        {
            float nz = P.noise;
            return Paint(w, h, 2f, ch, (u, v, xm, ym) =>
            {
                float n = Fbm(u, v, 8, Mathf.Max(1, Mathf.RoundToInt(4 * ch)), 101);
                Color c = Shade(P.plaster, n, nz * 0.6f);
                if (ym > ch - 0.08f) c = Mul(P.plaster, 0.9f); // cornice under the ceiling
                switch (style)
                {
                    case 0:
                        if (ym < P.wainscotHeight)
                        {
                            c = Shade(P.wainscot, n, nz);
                            if (Near(xm, 0.5f, Line * 0.5f)) c = Mul(P.wainscot, 0.85f);
                        }
                        if (ym >= P.wainscotHeight && ym < P.wainscotHeight + 0.05f) c = P.trim;
                        if (ym < 0.1f) c = Shade(P.baseboard, n, nz);
                        break;
                    case 1:
                        if (ym < P.wallTileHeight)
                        {
                            c = Shade(P.wallTile, Rand(Mathf.FloorToInt(xm / 0.2f), Mathf.FloorToInt(ym / 0.2f), 7), nz * 0.5f);
                            if (Near(xm, 0.2f, Line * 0.5f) || Near(ym, 0.2f, Line * 0.5f)) c = P.wallTileGrout;
                        }
                        if (ym >= P.wallTileHeight && ym < P.wallTileHeight + 0.04f) c = P.wallTileGrout;
                        break;
                    case 2:
                        if (ym < P.gymPanelHeight)
                        {
                            int board = Mathf.FloorToInt(xm / 0.1f);
                            c = Shade(P.gymPanel, Fbm(u * 0.25f, v, 4, 64, 300 + board) * 0.6f + Rand(board, 0, 9) * 0.4f, nz * 1.4f);
                            if (Near(xm, 0.1f, Line * 0.5f)) c = Mul(P.gymPanel, 0.75f);
                        }
                        if (ym >= P.gymPanelHeight && ym < P.gymPanelHeight + 0.06f) c = P.trim;
                        if (ym < 0.1f) c = Shade(P.baseboard, n, nz);
                        break;
                    case 3:
                        c = Mul(c, 0.9f); // facades sit in direct sun, keep them a notch darker
                        if (ym < 0.5f) c = Shade(P.facadePlinth, Fbm(u, v, 16, 32, 77), nz * 1.5f);
                        else if (Near(ym - 0.5f, 1.4f, Line * 0.5f)) c = Mul(P.plaster, 0.8f); // panel seams
                        break;
                }
                return c;
            });
        }

        static Img CorridorTex(int w)
        {
            // 0.5 m linoleum squares, two alternating shades, thin seams.
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                int i = Mathf.FloorToInt(xm / 0.5f), j = Mathf.FloorToInt(ym / 0.5f);
                Color c = ((i + j) & 1) == 0 ? P.corridorFloor : P.corridorFloorAlt;
                c = Shade(c, Fbm(u, v, 8, 8, 13), P.noise);
                if (Near(xm, 0.5f, Line * 0.5f) || Near(ym, 0.5f, Line * 0.5f)) c = Mul(c, 0.8f);
                return c;
            });
        }

        static Img PlankTex(int w, Color wood, float plankW, int seed)
        {
            // Planks run along Z (texture V); each 1 m long, rows offset by half a plank.
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                int row = Mathf.FloorToInt(xm / plankW);
                float off = (row % 2) * 0.5f;
                int plank = Mathf.FloorToInt((ym + off) / 1f);
                float tone = Rand(row, plank, seed);
                float grain = Fbm(u * 1f, v, 64, 4, seed + 5);
                Color c = Shade(Mul(wood, 0.9f + tone * 0.2f), grain, P.noise * 1.2f);
                if (Near(xm, plankW, Line * 0.5f) || Near(ym + off, 1f, Line * 0.5f)) c = Mul(wood, 0.6f);
                return c;
            });
        }

        static Img TileTex(int w, float size, float tile, Color face, Color grout, int seed)
        {
            return Paint(w, w, size, size, (u, v, xm, ym) =>
            {
                int i = Mathf.FloorToInt(xm / tile), j = Mathf.FloorToInt(ym / tile);
                Color c = Shade(face, Rand(i, j, seed), P.noise * 0.6f);
                if (Near(xm, tile, Line * 0.6f) || Near(ym, tile, Line * 0.6f)) c = grout;
                return c;
            });
        }

        static Img CarpetTex(int w)
        {
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                float lx = Mathf.Repeat(xm, 0.25f) - 0.125f, ly = Mathf.Repeat(ym, 0.25f) - 0.125f;
                bool diamond = Mathf.Abs(lx) + Mathf.Abs(ly) < 0.05f;
                Color c = diamond ? P.carpetPattern : P.carpet;
                return Shade(c, Rand(Mathf.FloorToInt(u * w), Mathf.FloorToInt(v * w), 5) * 0.5f + Fbm(u, v, 8, 8, 6) * 0.5f, P.noise * 1.3f);
            });
        }

        static Img GroundTex(int w)
        {
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                float n = Fbm(u, v, 6, 6, 60, 4);
                Color c = Shade(P.ground, n, P.noise * 2.2f);
                float speck = Rand(Mathf.FloorToInt(u * w), Mathf.FloorToInt(v * w), 61);
                if (speck > 0.965f) c = Shade(P.groundSpeck, speck, 0.2f);
                else if (speck < 0.03f) c = Mul(P.ground, 0.7f);
                return c;
            });
        }

        static Img ConcreteTex(int w)
        {
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                Color c = Shade(P.concrete, Fbm(u, v, 8, 8, 70, 4), P.noise * 1.5f);
                if (Rand(Mathf.FloorToInt(u * w), Mathf.FloorToInt(v * w), 71) > 0.97f) c = Mul(c, 0.85f);
                if (Near(xm, 1f, Line * 0.5f) || Near(ym, 1f, Line * 0.5f)) c = Mul(P.concrete, 0.8f);
                return c;
            });
        }

        static Img CeilingTex(int w)
        {
            return Paint(w, w, 2f, 2f, (u, v, xm, ym) =>
            {
                Color c = Shade(P.ceiling, Fbm(u, v, 8, 8, 80), P.noise * 0.5f);
                if (Rand(Mathf.FloorToInt(u * w), Mathf.FloorToInt(v * w), 81) > 0.9f) c = Mul(c, 0.92f); // acoustic dots
                if (Near(xm, 0.5f, Line * 0.6f) || Near(ym, 0.5f, Line * 0.6f)) c = P.ceilingLine;
                return c;
            });
        }

        static Img SlidingDoorTex(float dw, float dh)
        {
            int w = Mathf.RoundToInt(Ppm * dw), h = Mathf.RoundToInt(Ppm * dh);
            const float frame = 0.09f;
            return Paint(w, h, dw, dh, (u, v, xm, ym) =>
            {
                float n = Fbm(u, v, 4, 16, 90);
                bool border = xm < frame || xm > dw - frame || ym < frame || ym > dh - frame;
                bool glass = xm > 0.15f && xm < dw - 0.15f && ym > 1.25f && ym < dh - 0.25f;
                bool rail = ym > 1.15f && ym < 1.25f;
                Color c = Shade(P.slidingDoorPanel, n, P.noise);
                if (glass)
                {
                    c = Shade(P.doorGlass, v, 0.15f);
                    if (Mathf.Abs(xm - dw * 0.5f) < 0.02f) c = P.slidingDoorFrame; // muntin
                }
                if (border || rail) c = Shade(P.slidingDoorFrame, n, P.noise);
                return c;
            });
        }

        static Img SwingDoorTex(float dw, float dh)
        {
            int w = Mathf.RoundToInt(Ppm * dw), h = Mathf.RoundToInt(Ppm * dh);
            return Paint(w, h, dw, dh, (u, v, xm, ym) =>
            {
                float n = Fbm(u, v, 4, 16, 95);
                Color c = Shade(P.swingDoor, n, P.noise);
                bool inset = xm > 0.18f && xm < dw - 0.18f && ((ym > 0.4f && ym < 1.1f) || (ym > 1.3f && ym < dh - 0.3f));
                bool insetEdge = inset && (xm < 0.18f + Line * 2 || xm > dw - 0.18f - Line * 2);
                if (inset) c = Shade(P.swingDoorPanel, n, P.noise);
                if (insetEdge) c = Mul(P.swingDoor, 0.8f);
                if (ym < 0.25f) c = Shade(P.darkMetal, n, P.noise); // kick plate
                return c;
            });
        }

        static Img BlackboardTex(int w)
        {
            return Paint(w, w, 1f, 1f, (u, v, xm, ym) =>
            {
                Color c = Shade(P.blackboard, Fbm(u, v, 4, 4, 110), P.noise);
                float smudge = Fbm(u, v, 3, 2, 111, 4);
                if (smudge > 0.6f) c = Color.Lerp(c, P.chalk, (smudge - 0.6f) * 0.5f); // erased chalk
                return c;
            });
        }

        static Img VendingTex(float fw, float fh)
        {
            int w = Mathf.RoundToInt(Ppm * fw * 2), h = Mathf.RoundToInt(Ppm * fh * 2);
            Color[] cans = { P.magma, P.sunset, P.linoleum, P.gum, P.haze, P.chalk, P.dusk };
            return Paint(w, h, fw, fh, (u, v, xm, ym) =>
            {
                Color c = P.chalk;
                // Display window: 6 columns x 3 shelves of cans on a dark back.
                if (xm > 0.08f && xm < 0.68f && ym > 0.7f && ym < 1.65f)
                {
                    c = Mul(P.ash, 1.3f);
                    float lx = (xm - 0.08f) / 0.1f, ly = (ym - 0.7f) / (0.95f / 3f);
                    int col = Mathf.FloorToInt(lx), row = Mathf.FloorToInt(ly);
                    float fx = lx - col, fy = ly - row;
                    if (fx > 0.2f && fx < 0.8f && fy > 0.25f && fy < 0.8f)
                        c = Shade(cans[(col * 3 + row * 5) % cans.Length], Mathf.Abs(fx - 0.5f), -0.6f);
                    if (fy > 0.08f && fy < 0.18f) c = P.chalk; // price strip
                    if (fy <= 0.06f) c = P.metal;          // shelf
                }
                else if (xm > 0.75f && xm < 0.92f && ym > 0.9f && ym < 1.4f)
                {
                    c = P.darkMetal;
                    float by = Mathf.Repeat(ym - 0.9f, 0.1f);
                    if (by > 0.03f && by < 0.07f && xm > 0.79f && xm < 0.88f) c = P.sunset;
                }
                else if (ym < 0.4f) c = P.ash; // pickup slot
                return c;
            });
        }

        static Img NoiseTex(int w, int seed, float _)
        {
            return Paint(w, w, 1f, 1f, (u, v, xm, ym) =>
            {
                float n = Fbm(u, v, 4, 4, seed) * 0.7f + Rand(Mathf.FloorToInt(u * w), Mathf.FloorToInt(v * w), seed + 1) * 0.3f;
                float k = 1f - (1f - n) * P.noise * 2.2f;
                return new Color(k, k, k, 1f);
            });
        }

        static Img GrainTex(int w)
        {
            return Paint(w, w, 1f, 1f, (u, v, xm, ym) =>
            {
                float g = Fbm(u, v, 2, 32, 120, 3);
                float k = 1f - (1f - g) * P.noise * 3.5f;
                return new Color(k, k * 0.98f, k * 0.95f, 1f);
            });
        }

        // ---------- meshes ----------

        static Vector3 V(float x, float y, float z) => new(x, y, z);

        static Dictionary<string, Mesh> BuildMeshes()
        {
            Directory.CreateDirectory(MeshDir);
            var d = new Dictionary<string, Mesh>();
            void Add(string name, Action<KitMeshBuilder> build)
            {
                var b = new KitMeshBuilder();
                build(b);
                string path = MeshDir + "SM_School_" + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool isNew = mesh == null;
                if (isNew) mesh = new Mesh { name = "SM_School_" + name };
                b.FillMesh(mesh);
                if (isNew) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);
                d[name] = mesh;
            }

            // Desk 0.6 x 0.7 x 0.45: wooden top, steel tube frame and book tray. Sub 0 wood, 1 steel.
            Add("Desk", b =>
            {
                const float t = 0.03f, w = 0.6f, dp = 0.45f, top = 0.67f;
                b.Box(V(0, top, 0), V(w, top + 0.03f, dp), 0, false);
                foreach (var p in new[] { V(0.02f, 0, 0.02f), V(w - 0.02f - t, 0, 0.02f), V(0.02f, 0, dp - 0.02f - t), V(w - 0.02f - t, 0, dp - 0.02f - t) })
                    b.Box(p, p + V(t, top, t), 1, false);
                b.Box(V(0.05f, 0.55f, 0.03f), V(w - 0.05f, 0.57f, dp - 0.03f), 1, false);        // tray floor
                b.Box(V(0.05f, 0.57f, 0.03f), V(w - 0.05f, 0.63f, 0.05f), 1, false);              // tray back
                b.Box(V(0.02f, 0.1f, 0.03f), V(0.02f + t, 0.12f, dp - 0.03f), 1, false);          // foot rails
                b.Box(V(w - 0.02f - t, 0.1f, 0.03f), V(w - 0.02f, 0.12f, dp - 0.03f), 1, false);
                b.Box(V(w, 0.5f, 0.2f), V(w + 0.02f, 0.53f, 0.24f), 1, false);                    // bag hook
            });

            // Chair 0.4 x 0.8 x 0.4: plywood seat and back on a tube frame.
            Add("Chair", b =>
            {
                const float t = 0.025f, s = 0.4f;
                b.Box(V(0.01f, 0.4f, 0.01f), V(s - 0.01f, 0.425f, s - 0.02f), 0, false);
                b.Box(V(0.03f, 0.56f, 0.36f), V(s - 0.03f, 0.78f, 0.385f), 0, false);
                foreach (var p in new[] { V(0.02f, 0, 0.02f), V(s - 0.02f - t, 0, 0.02f) })
                    b.Box(p, p + V(t, 0.4f, t), 1, false);
                foreach (var p in new[] { V(0.02f, 0, s - 0.02f - t), V(s - 0.02f - t, 0, s - 0.02f - t) })
                    b.Box(p, p + V(t, 0.8f, t), 1, false); // back legs carry the backrest
                b.Box(V(0.02f, 0.08f, 0.02f), V(0.02f + t, 0.1f, s - 0.02f), 1, false);
                b.Box(V(s - 0.02f - t, 0.08f, 0.02f), V(s - 0.02f, 0.1f, s - 0.02f), 1, false);
            });

            // Teacher's desk 1.2 x 0.75 x 0.7: steel body, laminate top, drawer pedestal with handles.
            Add("TeacherDesk", b =>
            {
                b.Box(V(-0.01f, 0.72f, -0.01f), V(1.21f, 0.75f, 0.71f), 0, false);
                b.Box(V(0, 0, 0), V(0.05f, 0.72f, 0.7f), 1, false);
                b.Box(V(0.05f, 0.2f, 0.6f), V(1.15f, 0.72f, 0.65f), 1, false);
                b.Box(V(0.75f, 0, 0.02f), V(1.2f, 0.72f, 0.7f), 1, false);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.1f + i * 0.21f;
                    b.Box(V(0.78f, y + 0.18f, 0.0f), V(1.17f, y + 0.19f, 0.02f), 2, false);   // drawer seam
                    b.Box(V(0.9f, y + 0.09f, -0.02f), V(1.05f, y + 0.11f, 0.02f), 2, false);  // handle
                }
            });

            // Blackboard 3.6 m: green board, wooden frame, steel chalk tray with chalk.
            Add("Blackboard", b =>
            {
                b.Box(V(0.05f, 0.95f, 0), V(3.55f, 2.05f, 0.03f), 0, false);
                b.Box(V(0, 0.9f, 0), V(3.6f, 0.95f, 0.045f), 1, false);
                b.Box(V(0, 2.05f, 0), V(3.6f, 2.1f, 0.045f), 1, false);
                b.Box(V(0, 0.95f, 0), V(0.05f, 2.05f, 0.045f), 1, false);
                b.Box(V(3.55f, 0.95f, 0), V(3.6f, 2.05f, 0.045f), 1, false);
                b.Box(V(0.2f, 0.88f, 0), V(3.4f, 0.9f, 0.1f), 2, false);
                b.Box(V(0.2f, 0.9f, 0.08f), V(3.4f, 0.92f, 0.1f), 2, false);
                b.Box(V(0.6f, 0.9f, 0.04f), V(0.68f, 0.912f, 0.052f), 3, false);
                b.Box(V(0.75f, 0.9f, 0.05f), V(0.81f, 0.912f, 0.062f), 3, false);
                b.Box(V(2.9f, 0.9f, 0.03f), V(3.05f, 0.94f, 0.07f), 2, false); // eraser
            });

            // Courtyard bench 2 m: three slats on steel legs.
            Add("Bench", b =>
            {
                for (int i = 0; i < 3; i++)
                    b.Box(V(0, 0.42f, 0.01f + i * 0.13f), V(2f, 0.45f, 0.12f + i * 0.13f), 0, false);
                foreach (float x in new[] { 0.1f, 1.85f })
                {
                    b.Box(V(x, 0, 0.05f), V(x + 0.05f, 0.42f, 0.08f), 1, false);
                    b.Box(V(x, 0, 0.32f), V(x + 0.05f, 0.42f, 0.35f), 1, false);
                    b.Box(V(x, 0.38f, 0.05f), V(x + 0.05f, 0.42f, 0.35f), 1, false);
                }
                b.Box(V(0.1f, 0.12f, 0.19f), V(1.9f, 0.15f, 0.21f), 1, false);
            });

            // Dorm bunk bed: steel frame, mattresses, pillows.
            Add("BunkBed", b =>
            {
                const float bw = 0.9f, bl = 2f, bh = 1.8f, p = 0.05f;
                foreach (var c in new[] { V(0, 0, 0), V(bw - p, 0, 0), V(0, 0, bl - p), V(bw - p, 0, bl - p) })
                    b.Box(c, c + V(p, bh, p), 1, false);
                foreach (float y in new[] { 0.3f, 1.3f })
                {
                    b.Box(V(0, y, 0), V(bw, y + 0.06f, p), 1, false);
                    b.Box(V(0, y, bl - p), V(bw, y + 0.06f, bl), 1, false);
                    b.Box(V(0, y, 0), V(p, y + 0.06f, bl), 1, false);
                    b.Box(V(bw - p, y, 0), V(bw, y + 0.06f, bl), 1, false);
                    b.Box(V(p, y + 0.04f, p), V(bw - p, y + 0.2f, bl - p), 0, false);           // mattress
                    b.Box(V(0.15f, y + 0.2f, 0.08f), V(bw - 0.15f, y + 0.3f, 0.45f), 2, false); // pillow
                }
                b.Box(V(bw - 0.03f, 1.5f, 0.6f), V(bw, 1.75f, bl), 1, false);
                for (int i = 1; i <= 3; i++)
                    b.Box(V(0.2f, 0.3f * i + 0.2f, bl - 0.03f), V(0.6f, 0.3f * i + 0.23f, bl), 1, false);
            });

            // Toilet stall: partitions and door (stall color), bowl (porcelain), latch.
            Add("ToiletStall", b =>
            {
                const float sw = 1f, sd = 1.5f, sh = 2f, p = 0.03f, gap = 0.15f;
                b.Box(V(0, gap, 0), V(p, sh, sd), 0, false);
                b.Box(V(sw - p, gap, 0), V(sw, sh, sd), 0, false);
                b.Box(V(p, gap, sd - p), V(sw - p, sh, sd), 0, false);
                b.Box(V(0.32f, 0, 0.1f), V(0.68f, 0.38f, 0.6f), 1, false);    // bowl
                b.Box(V(0.3f, 0.38f, 0.08f), V(0.7f, 0.41f, 0.65f), 1, false); // seat
                b.Box(V(0.3f, 0.4f, 0), V(0.7f, 0.8f, 0.2f), 1, false);        // tank
                b.Box(V(0.6f, 0.8f, 0.05f), V(0.66f, 0.83f, 0.12f), 2, false); // flush lever
                b.Box(V(0.85f, 1f, sd - 0.06f), V(0.92f, 1.06f, sd + 0.02f), 2, false);
            });

            // Trash bin 0.6 x 1.06: plastic body, darker rim and lid slot.
            Add("TrashBin", b =>
            {
                b.Box(V(0.02f, 0, 0.02f), V(0.58f, 0.95f, 0.58f), 0, false);
                b.Box(V(-0.02f, 0.95f, -0.02f), V(0.62f, 1.06f, 0.62f), 1, false);
                b.Box(V(0.15f, 1.06f, 0.2f), V(0.45f, 1.07f, 0.4f), 0, false);
            });

            // Floor slabs: the kit mesh with its downward faces moved to slot 1 (ceiling below).
            foreach (var floor in new[] { "Floor", "Floor_1x1" })
            {
                var src = AssetDatabase.LoadAssetAtPath<Mesh>(KitMeshDir + "SM_Kit_" + floor + ".asset");
                if (src == null) continue;
                string path = MeshDir + "SM_School_" + floor + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool isNew = mesh == null;
                if (isNew) mesh = new Mesh { name = "SM_School_" + floor };
                SplitUnderside(src, mesh);
                if (isNew) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);
                d[floor] = mesh;
            }

            // Window glass and frame for the hole of SM_Kit_WallWindow (Sliding two-pane window).
            var hole = WindowHole();
            Add("WindowGlass", b =>
            {
                float x0 = hole.xMin, x1 = hole.xMax, y0 = hole.yMin, y1 = hole.yMax;
                float zc = WallThickness() * 0.5f, f = 0.05f, d = 0.03f, xm = (x0 + x1) * 0.5f;
                b.Box(V(x0, y0, zc - d), V(x0 + f, y1, zc + d), 0, false);
                b.Box(V(x1 - f, y0, zc - d), V(x1, y1, zc + d), 0, false);
                b.Box(V(x0, y0, zc - d), V(x1, y0 + f, zc + d), 0, false);
                b.Box(V(x0, y1 - f, zc - d), V(x1, y1, zc + d), 0, false);
                b.Box(V(xm - 0.025f, y0, zc - d - 0.01f), V(xm + 0.025f, y1, zc + d + 0.01f), 0, false);
                b.Box(V(x0 + f, y0 + f, zc - 0.004f), V(x1 - f, y1 - f, zc + 0.004f), 1, false);
            });

            AssetDatabase.SaveAssets();
            return d;
        }

        static void SplitUnderside(Mesh src, Mesh dst)
        {
            var verts = src.vertices;
            var normals = src.normals;
            var top = new List<int>();
            var bottom = new List<int>();
            var tris = src.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                bool down = normals[tris[i]].y < -0.5f;
                (down ? bottom : top).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }
            dst.Clear();
            dst.SetVertices(verts);
            dst.SetNormals(normals);
            dst.SetUVs(0, new List<Vector2>(src.uv));
            dst.subMeshCount = 2;
            dst.SetTriangles(top, 0);
            dst.SetTriangles(bottom, 1);
            dst.RecalculateBounds();
            dst.RecalculateTangents();
        }

        static Rect WindowHole()
        {
            // Read the hole straight from the kit mesh: its distinct X and Y coordinates are
            // 0, x0, x1, width and 0, sill, top, height.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(KitMeshDir + "SM_Kit_WallWindow.asset");
            if (mesh != null)
            {
                var xs = mesh.vertices.Select(v => Mathf.Round(v.x * 1000f) / 1000f).Distinct().OrderBy(x => x).ToList();
                var ys = mesh.vertices.Select(v => Mathf.Round(v.y * 1000f) / 1000f).Distinct().OrderBy(y => y).ToList();
                if (xs.Count == 4 && ys.Count == 4) return Rect.MinMaxRect(xs[1], ys[1], xs[2], ys[2]);
            }
            var kit = AssetDatabase.LoadAssetAtPath<KitSettings>("Assets/_Project/School/Kit/KitSettings.asset");
            float x0 = (kit.wallModuleWidth - kit.windowWidth) * 0.5f;
            return Rect.MinMaxRect(x0, kit.windowSillHeight, x0 + kit.windowWidth, kit.windowSillHeight + kit.windowHeight);
        }

        static float WallThickness()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(KitMeshDir + "SM_Kit_Wall.asset");
            return mesh != null ? mesh.bounds.size.z : 0.2f;
        }

        static bool AddWindowGlass(MeshRenderer wall, Mesh glassMesh)
        {
            var t = wall.transform.Find(WindowChild);
            bool created = t == null;
            if (created)
            {
                var go = new GameObject(WindowChild);
                go.layer = wall.gameObject.layer;
                t = go.transform;
                t.SetParent(wall.transform, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            t.GetComponent<MeshFilter>().sharedMesh = glassMesh;
            t.GetComponent<MeshRenderer>().sharedMaterials = new[] { M["WindowFrame"], M["WindowGlass"] };
            return created;
        }

        // ---------- scene access and backup ----------

        static Scene OpenSchool(out bool openedHere, out bool wasDirty)
        {
            var scene = SceneManager.GetSceneByPath(SchoolScenePath);
            openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SchoolScenePath, OpenSceneMode.Additive);
            wasDirty = scene.isDirty;
            return scene;
        }

        static IEnumerable<GameObject> RootsOf(Scene scene) =>
            scene.GetRootGameObjects().Where(g => Array.IndexOf(Roots, g.name) >= 0);

        [Serializable] class Backup { public List<Entry> entries = new(); }
        [Serializable] class Entry { public string id; public string mesh; public string[] mats; }

        static Backup LoadBackup() =>
            File.Exists(BackupPath) ? JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath)) ?? new Backup() : new Backup();

        static HashSet<string> _known;

        static void Remember(Backup backup, MeshRenderer mr, MeshFilter mf)
        {
            _known ??= new HashSet<string>();
            if (_known.Count == 0) foreach (var e in backup.entries) _known.Add(e.id);
            string id = GlobalObjectId.GetGlobalObjectIdSlow(mr).ToString();
            if (!_known.Add(id)) return; // keep the original look from the first Apply
            backup.entries.Add(new Entry { id = id, mesh = RefOf(mf.sharedMesh), mats = mr.sharedMaterials.Select(RefOf).ToArray() });
        }

        static void SaveBackup(Backup backup)
        {
            Directory.CreateDirectory(ArtDir);
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));
            AssetDatabase.ImportAsset(BackupPath);
            _known = null;
        }

        static string RefOf(UnityEngine.Object o) =>
            o != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long local) ? guid + ":" + local : "";

        static T LoadRef<T>(string r) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(r)) return null;
            var parts = r.Split(':');
            string path = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (string.IsNullOrEmpty(path)) return null;
            long local = long.Parse(parts[1]);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is T t && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long l) && l == local) return t;
            return null;
        }

        // ---------- screenshots ----------

        static readonly (string zone, float height, string label)[] ShotZones =
        {
            ("assembly_foyer", 1.6f, "entrance"), ("corridor_s_1f", 1.6f, "corridor"), ("heroes_class", 1.6f, "classroom"),
            ("wc_m_1f", 1.6f, "toilet"), ("gym", 2.2f, "gym"), ("courtyard", 5f, "courtyard"),
        };

        // Renders a few fixed views of the school into Docs/SchoolStyle/<prefix>_<label>.png.
        public static void Capture(string prefix)
        {
            var scene = OpenSchool(out _, out _);
            var zones = RootsOf(scene).SelectMany(r => r.GetComponentsInChildren<Zone>(true)).ToList();
            Directory.CreateDirectory(ShotsDir);
            var go = new GameObject("StyleShotCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            int n = 0;
            foreach (var (zoneId, height, label) in ShotZones)
            {
                var zone = zones.FirstOrDefault(z => z.zoneId == zoneId);
                var box = zone != null ? zone.GetComponent<BoxCollider>() : null;
                if (box == null) continue;
                var b = box.bounds;
                var from = new Vector3(b.min.x + 0.7f, b.min.y + height, b.min.z + 0.7f);
                var to = new Vector3(b.max.x - 1f, b.min.y + 1.1f, b.max.z - 1f);
                go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(ShotsDir + prefix + "_" + label + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                n++;
            }
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[SchoolStyle] {n} shots saved to {Path.GetFullPath(ShotsDir)} ({prefix}_*.png).");
        }

        [MenuItem("Tools/Funseki/School Style/Capture Before Shots", priority = 41)]
        static void CaptureBefore() => Capture("before");

        [MenuItem("Tools/Funseki/School Style/Capture After Shots", priority = 42)]
        static void CaptureAfter() => Capture("after");
    }
}
