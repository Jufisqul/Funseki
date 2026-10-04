using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Editor-only: builds the whole MainMenu scene (environment from Art/Environment/build_menu_env.py,
// lighting "Day 1 — clear morning", UI and wiring). Re-runnable: it rebuilds the scene from scratch.
public static class MainMenuBuilder
{
    const string Root = "Assets/MainMenu/";
    const string Models = Root + "Models/Env/";
    const string MatDir = Root + "Materials/";
    const string TexDir = Root + "Textures/";
    const string FontDir = Root + "Fonts/";

    // ---------------------------------------------------------------- palette (matches the reference board)
    static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
    {
        {"Wall","#DEDAD3"},{"WallLight","#ECE9E3"},{"WallShade","#B4B0A9"},{"Trim","#F2F0EB"},{"Concrete","#BDB8AE"},
        {"Roof","#8F8C88"},{"Interior","#3B4A63"},{"Glass","#9FD3EE"},{"GlassDark","#4A6A82"},{"Frosted","#E3ECEF"},
        {"Frame","#8E9AA0"},{"Curtain","#F4EAD2"},{"Plywood","#C9A26B"},{"Metal","#7A8288"},{"Green","#4F7F5A"},
        {"ChainLink","#5F7F68"},{"Tile","#7FB7B0"},{"DoorRed","#C8553D"},{"DoorBlue","#3E6FA8"},{"Black","#1C1B22"},
        {"White","#F4F4F4"},{"Red","#E23C3C"},{"Blue","#2F5D9E"},{"Yellow","#FFD23F"},{"Orange","#FF8A2B"},
        {"Gold","#D4A93A"},{"Wood","#8C6239"},{"WoodDark","#5B3E28"},{"Cork","#B98A5A"},{"TankWhite","#DADBD5"},
        {"ACWhite","#E6E6E1"},{"VendingRed","#D93A3A"},{"VendingLight","#FFF4D6"},{"TrashBag","#2B2F3A"},
        {"TrashBlue","#3E6FA8"},{"Cardboard","#B58A5A"},{"BikeFrame","#C0392B"},{"Tire","#26262B"},
        {"Pink","#F6A9C6"},{"PinkLight","#FBC9DA"},{"PinkDeep","#E58DB0"},{"Bark","#5A3B32"},
        {"Leaf","#7DBB5A"},{"LeafDark","#5E9E47"},{"HouseCream","#EFE6D2"},{"HouseGrey","#C9CCCF"},
        {"HouseBlue","#B8C7D3"},{"RoofBlue","#4F6476"},{"RoofBrown","#6B4A3A"},{"RoofDark","#3C3F48"},
        {"Awning","#3E8E7E"},{"LampGlow","#FFE9A8"},{"Beak","#3A3A40"},
        // Tomura Ryuta
        {"Jacket","#1C1D26"},{"Pants","#22232E"},{"Shirt","#F2F2F0"},{"Skin","#F2C9B0"},{"Hair","#E6BD5C"},
        {"Shoes","#2A211C"},{"Magazine","#C0392B"},{"Eyes","#2B2233"},{"Brows","#9C7A3C"},{"Mouth","#A5453F"},
        {"Cig","#F7F7F5"},{"Ember","#FF6A2A"},
        // scene-only
        {"Dirt","#D2C0A2"},{"Lawn","#A3B884"},{"Asphalt","#77767A"},{"Paving","#CFCAC0"},{"RoadLine","#F4F1E8"},
        {"Puddle","#8FB3CC"},{"Crack","#8E8577"},{"Graffiti","#FFFFFF"},{"Grime","#FFFFFF"},{"Wire","#26262B"},
        {"NamePlate","#8C6239"},
    };

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static TMP_FontAsset fontTitle, fontHeavy, fontBody, fontBodyBold;
    static Material titleMat;
    static Sprite plateSprite, softDot, whiteSprite;
    static System.Random rng;

    // ================================================================= menu items

    [MenuItem("Tools/One Funseki/1. Import TMP Essentials")]
    public static void ImportTmpEssentials()
    {
        if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) { Debug.Log("[MainMenuBuilder] TMP Essentials already present."); return; }
        string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        AssetDatabase.ImportPackage(pkg, false);
        Debug.Log("[MainMenuBuilder] Importing TMP Essentials from " + pkg);
    }

    [MenuItem("Tools/One Funseki/2. Build Main Menu")]
    public static void Build()
    {
        rng = new System.Random(19);
        mats.Clear();
        foreach (var d in new[] { MatDir, TexDir, Root + "UI" }) Directory.CreateDirectory(d);

        MakeTextures();
        MakeMaterials();
        SetupModelImports();
        MakeFonts();

        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

        var env = new GameObject("Environment").transform;
        BuildGround(Group(env, "Ground"));
        BuildSchool(Group(env, "School"));
        BuildYard(Group(env, "Yard"));
        BuildStreet(Group(env, "Street"));
        foreach (Transform t in env) SetStatic(t.gameObject);

        var lighting = BuildLighting(out Volume volume);
        var poses = BuildCameraPoses();
        var cam = BuildCamera(poses.Find("Home"));
        BuildMiniScenes(env);
        var audio = BuildAudio();
        BuildUI(cam, poses, volume, audio);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MainMenuBuilder] Main menu built.");
    }

    // ================================================================= assets

    static void MakeTextures()
    {
        // Anime morning sky: gradient, crisp cumulus clouds and hazy mountains at the horizon (equirect).
        const int W = 2048, Hh = 1024;
        var sky = new Texture2D(W, Hh, TextureFormat.RGBA32, false);
        Color zenith = Hex("#5FAEEB"), mid = Hex("#94D0F4"), horizon = Hex("#FFE6EE"), below = Hex("#EADCD3");
        Color farHill = Hex("#A9C7D6"), nearHill = Hex("#8FB4BE");
        var clouds = new List<Vector4>();
        for (int c = 0; c < 26; c++)
        {
            float cx = (float)rng.NextDouble(), cy = Mathf.Lerp(0.56f, 0.78f, (float)rng.NextDouble());
            int puffs = rng.Next(5, 10);
            float scale = Mathf.Lerp(0.012f, 0.03f, (float)rng.NextDouble()) * (1.2f - (cy - 0.56f) * 2f);
            for (int p = 0; p < puffs; p++)
                clouds.Add(new Vector4(cx + (p - puffs / 2f) * scale * 1.1f, cy + (float)rng.NextDouble() * scale * 0.9f,
                    scale * Mathf.Lerp(0.8f, 1.5f, (float)rng.NextDouble()), cy));
        }
        var px = new Color[W * Hh];
        for (int y = 0; y < Hh; y++)
        {
            float v = (y + 0.5f) / Hh;
            Color baseC = v < 0.5f ? Color.Lerp(below, horizon, Mathf.Pow(v / 0.5f, 6f))
                : v < 0.62f ? Color.Lerp(horizon, mid, Mathf.SmoothStep(0, 1, (v - 0.5f) / 0.12f))
                : Color.Lerp(mid, zenith, Mathf.SmoothStep(0, 1, (v - 0.62f) / 0.3f));
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                Color c = baseC;
                float a = u * Mathf.PI * 2f;
                float far = 0.5f + 0.022f + 0.012f * Mathf.Sin(a * 3f + 1f) + 0.008f * Mathf.Sin(a * 7f + 2f) + 0.004f * Mathf.Sin(a * 17f);
                float near = 0.5f + 0.01f + 0.01f * Mathf.Sin(a * 5f + 4f) + 0.005f * Mathf.Sin(a * 11f + 1f);
                if (v > 0.5f && v < far) c = Color.Lerp(farHill, horizon, 0.35f);
                if (v > 0.5f && v < near) c = nearHill;
                float cloud = 0f, shade = 0f;
                foreach (var cl in clouds)
                {
                    float du = Mathf.Abs(u - cl.x); du = Mathf.Min(du, 1f - du);
                    float d = Mathf.Sqrt(du * du * 0.25f + (v - cl.y) * (v - cl.y)) / cl.z;
                    float k = Mathf.Clamp01((1f - d) * 18f);
                    if (k > cloud) { cloud = k; shade = Mathf.Clamp01((cl.y - v) / cl.z * 0.9f); }
                }
                if (cloud > 0f)
                {
                    Color cc = Color.Lerp(Color.white, Hex("#DCE3F2"), shade * 0.85f);
                    c = Color.Lerp(c, cc, cloud);
                }
                px[y * W + x] = c;
            }
        }
        sky.SetPixels(px);
        SaveTexture(sky, "Sky_Day1", t => { t.wrapModeU = TextureWrapMode.Repeat; t.wrapModeV = TextureWrapMode.Clamp; t.mipmapEnabled = false; t.maxTextureSize = 2048; });

        // Chain-link diamonds (alpha).
        var chain = new Texture2D(64, 64, TextureFormat.RGBA32, true);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float a = Mathf.Abs(((x + y) % 32) - 16f), b = Mathf.Abs(((x - y + 64) % 32) - 16f);
                float wire = Mathf.Clamp01(2.2f - Mathf.Min(16f - a, 16f - b));
                chain.SetPixel(x, y, new Color(1, 1, 1, wire));
            }
        SaveTexture(chain, "ChainLink", t => t.alphaIsTransparency = true);

        // Ground variation (tileable value noise) for dirt and asphalt.
        SaveTexture(NoiseTex(256, 0.86f, 1.0f, 24, true), "DirtNoise", null);
        SaveTexture(NoiseTex(256, 0.9f, 1.0f, 64, false), "AsphaltNoise", null);

        // UI: skewed plate, soft dot, white.
        var plate = new Texture2D(512, 96, TextureFormat.RGBA32, false);
        for (int y = 0; y < 96; y++)
            for (int x = 0; x < 512; x++)
            {
                float slant = 22f * (y / 95f);
                float l = x - slant, r = (512 - 22f + slant) - x;
                plate.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(Mathf.Min(l, r) + 0.5f)));
            }
        plateSprite = SaveSprite(plate, "UI_Plate", new Vector4(30, 0, 30, 0));
        var dot = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)));
            }
        softDot = SaveSprite(dot, "UI_SoftDot", Vector4.zero);
        var white = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int i = 0; i < 64; i++) white.SetPixel(i % 8, i / 8, Color.white);
        whiteSprite = SaveSprite(white, "UI_White", new Vector4(2, 2, 2, 2));

        // Reuse the graffiti/grime textures generated earlier, if they are still around.
        foreach (var (from, to) in new[] { ("Assets/MainMenu/Generated/Textures/Graffiti_RGBA.png", TexDir + "Graffiti.png"),
                                           ("Assets/MainMenu/Generated/Textures/GrimeStreaks.png", TexDir + "GrimeStreaks.png") })
            if (File.Exists(from) && !File.Exists(to)) AssetDatabase.CopyAsset(from, to);
    }

    static Texture2D NoiseTex(int size, float lo, float hi, int cells, bool pebbles)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var lattice = new float[cells, cells];
        for (int i = 0; i < cells; i++) for (int j = 0; j < cells; j++) lattice[i, j] = (float)rng.NextDouble();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x * cells / (float)size, fy = y * cells / (float)size;
                int x0 = (int)fx, y0 = (int)fy;
                float tx = Mathf.SmoothStep(0, 1, fx - x0), ty = Mathf.SmoothStep(0, 1, fy - y0);
                float n = Mathf.Lerp(Mathf.Lerp(lattice[x0 % cells, y0 % cells], lattice[(x0 + 1) % cells, y0 % cells], tx),
                                     Mathf.Lerp(lattice[x0 % cells, (y0 + 1) % cells], lattice[(x0 + 1) % cells, (y0 + 1) % cells], tx), ty);
                float g = Mathf.Lerp(lo, hi, n);
                if (pebbles && rng.NextDouble() < 0.004) g *= 0.82f;
                t.SetPixel(x, y, new Color(g, g, g, 1));
            }
        return t;
    }

    static void MakeMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var kv in Palette)
        {
            var m = LoadOrCreate(MatDir + kv.Key + ".mat", lit);
            m.SetColor("_BaseColor", Hex(kv.Value));
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mats[kv.Key] = m;
        }
        void Shiny(string n, float s) { var m = mats[n]; m.SetFloat("_Smoothness", s); m.SetFloat("_EnvironmentReflections", 1f); m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); m.SetFloat("_SpecularHighlights", 1f); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        Shiny("Glass", 0.9f); Shiny("GlassDark", 0.85f); Shiny("Puddle", 0.95f); Shiny("TrashBag", 0.6f); Shiny("TrashBlue", 0.55f);
        Shiny("VendingRed", 0.5f); Shiny("Metal", 0.4f); Shiny("Gold", 0.6f); mats["Gold"].SetFloat("_Metallic", 0.6f);
        Transparent(mats["Glass"]); mats["Glass"].SetColor("_BaseColor", Hex("#9FD3EE", 0.72f));
        Transparent(mats["Puddle"]); mats["Puddle"].SetColor("_BaseColor", Hex("#8FB3CC", 0.65f));
        foreach (var n in new[] { "VendingLight", "LampGlow", "Ember" })
        {
            var m = mats[n];
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(Palette[n]) * (n == "Ember" ? 3f : 1.6f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        Textured("ChainLink", TexDir + "ChainLink.png", new Vector2(8, 8)); Clip(mats["ChainLink"], 0.5f); mats["ChainLink"].SetFloat("_Cull", 0);
        Textured("Dirt", TexDir + "DirtNoise.png", new Vector2(18, 12));
        Textured("Asphalt", TexDir + "AsphaltNoise.png", new Vector2(40, 2));
        Textured("Graffiti", TexDir + "Graffiti.png", Vector2.one); Clip(mats["Graffiti"], 0.4f);
        Textured("Grime", TexDir + "GrimeStreaks.png", Vector2.one); Transparent(mats["Grime"]);
        mats["Grime"].SetColor("_BaseColor", new Color(1, 1, 1, 0.55f));
        foreach (var m in mats.Values) EditorUtility.SetDirty(m);
    }

    static void Textured(string name, string tex, Vector2 tiling)
    {
        var m = mats[name];
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetTextureScale("_BaseMap", tiling);
    }

    static void Clip(Material m, float cutoff)
    {
        m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest;
    }

    static void Transparent(Material m)
    {
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", 1); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
    }

    static void SetupModelImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "Models" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.importCameras = false; mi.importLights = false;
            foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
        }
    }

    static void MakeFonts()
    {
        fontTitle = FontAsset("Unbounded-ExtraBold");
        fontHeavy = FontAsset("Unbounded-Medium");
        fontBody = FontAsset("GolosText-Regular");
        fontBodyBold = FontAsset("GolosText-SemiBold");
        string path = FontDir + "Unbounded-ExtraBold Title.mat";
        titleMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!titleMat) { titleMat = new Material(fontTitle.material); AssetDatabase.CreateAsset(titleMat, path); }
        titleMat.shader = fontTitle.material.shader;
        titleMat.SetTexture("_MainTex", fontTitle.material.GetTexture("_MainTex"));
        titleMat.EnableKeyword("OUTLINE_ON");
        titleMat.SetFloat("_OutlineWidth", 0.16f);
        titleMat.SetColor("_OutlineColor", Hex("#1C1B22"));
        titleMat.EnableKeyword("UNDERLAY_ON");
        titleMat.SetColor("_UnderlayColor", new Color(0.11f, 0.1f, 0.14f, 0.85f));
        titleMat.SetFloat("_UnderlayOffsetX", 0.6f);
        titleMat.SetFloat("_UnderlayOffsetY", -0.6f);
        titleMat.SetFloat("_UnderlayDilate", 0.4f);
        EditorUtility.SetDirty(titleMat);
    }

    static TMP_FontAsset FontAsset(string name)
    {
        string path = FontDir + name + " SDF.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fa) return fa;
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + name + ".ttf");
        fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = name + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.material.name = name + " SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        foreach (var t in fa.atlasTextures) { t.name = name + " SDF Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
        fa.TryAddCharacters(" !\"#%&'()*+,-./0123456789:;<=>?«»—–…№×‹›ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя", out _);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // ================================================================= environment

    static void BuildGround(Transform root)
    {
        Slab(root, "Lawn", new Vector3(0, -0.13f, 0), new Vector3(400, 0.2f, 400), "Lawn");
        Slab(root, "Yard", new Vector3(0, -0.1f, 5.5f), new Vector3(68, 0.2f, 45), "Dirt");
        Slab(root, "Walk", new Vector3(0, 0.01f, -2.4f), new Vector3(5.2f, 0.04f, 29.2f), "Paving");
        Slab(root, "Apron", new Vector3(0, 0.012f, 15.0f), new Vector3(34.6f, 0.04f, 2.2f), "Paving");
        Slab(root, "WingApron", new Vector3(13.0f, 0.012f, 4f), new Vector3(2.2f, 0.04f, 24f), "Paving");
        Slab(root, "Sidewalk", new Vector3(0, 0.06f, -18.6f), new Vector3(160, 0.12f, 2.8f), "Paving");
        Slab(root, "Road", new Vector3(0, -0.09f, -23.5f), new Vector3(160, 0.2f, 7f), "Asphalt");
        Slab(root, "SidewalkFar", new Vector3(0, 0.06f, -28f), new Vector3(160, 0.12f, 2f), "Paving");
        for (float x = -78; x < 80; x += 4.5f) Slab(root, "Dash", new Vector3(x, 0.012f, -23.5f), new Vector3(2.2f, 0.01f, 0.14f), "RoadLine");
        foreach (float z in new[] { -20.3f, -26.7f }) Slab(root, "EdgeLine", new Vector3(0, 0.012f, z), new Vector3(160, 0.01f, 0.12f), "RoadLine");
        // Puddles and cracks: a tired yard.
        foreach (var p in new[] { new Vector4(-5.5f, -4.5f, 2.4f, 1.3f), new Vector4(5.8f, 3.5f, 1.8f, 1.0f), new Vector4(-10.5f, -9.5f, 2.8f, 1.5f), new Vector4(1.2f, -12.5f, 1.4f, 0.8f) })
        {
            var pd = Prim(root, PrimitiveType.Cylinder, "Puddle", new Vector3(p.x, 0.008f, p.y), new Vector3(0, (float)rng.NextDouble() * 180, 0), new Vector3(p.z, 0.004f, p.w), "Puddle");
            pd.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = new Vector3(Rand(-2.2f, 2.2f), 0.033f, Rand(-15f, 11f));
            float yaw = Rand(0, 180);
            for (int s = 0; s < 4; s++)
            {
                float len = Rand(0.3f, 0.8f);
                yaw += Rand(-35, 35);
                var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Slab(root, "Crack", p + dir * len / 2, new Vector3(0.03f, 0.004f, len), "Crack").transform.rotation = Quaternion.Euler(0, yaw, 0);
                p += dir * len;
            }
        }
    }

    static void BuildSchool(Transform root)
    {
        Place(root, "School_Main", new Vector3(0, 0, 16), 180);
        Place(root, "School_Wing", new Vector3(14, 0, 4), -90);
        // Graffiti and grime on the main facade plinth and the wing wall.
        foreach (float x in new[] { -13.5f, -8.2f, 7.6f, 12.8f })
            Decal(root, "Graffiti", new Vector3(x, Rand(0.45f, 0.65f), 15.97f), Vector3.back, Rand(1.3f, 1.8f), rng.Next(16));
        Decal(root, "Graffiti", new Vector3(13.97f, 1.6f, -1.6f), Vector3.left, 2.6f, 3);
        Decal(root, "Graffiti", new Vector3(13.97f, 1.1f, 3.1f), Vector3.left, 1.6f, 8);
        Decal(root, "Graffiti", new Vector3(13.97f, 0.7f, -4.6f), Vector3.left, 1.3f, 13);
        for (int i = 0; i < 7; i++)
            Decal(root, "Grime", new Vector3(13.965f, 3.4f * (1 + i % 2) - 0.8f, Rand(-7, 15)), Vector3.left, Rand(0.9f, 1.6f), -1, 1.5f);
    }

    static void BuildYard(Transform root)
    {
        // Toilet block and bike parking (left, "Коллекция").
        var toilet = Place(root, "ToiletBlock", new Vector3(-14, 0, 6), 90);
        var doorClosed = Place(toilet.transform, "ToiletDoor", Vector3.zero, 0);
        doorClosed.transform.localPosition = B(-1.6f - 0.5f, -0.1f, 0);
        doorClosed.transform.localRotation = Quaternion.identity;
        Swap(doorClosed, "DoorRed", "DoorBlue");
        var doorOpen = Place(toilet.transform, "ToiletDoor", Vector3.zero, 0);
        doorOpen.name = "ToiletDoor_Creaking";
        doorOpen.transform.localPosition = B(1.6f - 0.5f, -0.1f, 0);
        doorOpen.transform.localRotation = Quaternion.Euler(0, 38, 0);
        var creak = doorOpen.AddComponent<LoopMotion>(); creak.swayDegrees = 7; creak.frequency = 0.11f;
        Decal(root, "Graffiti", new Vector3(-13.97f, 0.55f, 6f), Vector3.right, 0.9f, 5);
        Decal(root, "Graffiti", new Vector3(-16.2f, 1.6f, 2.47f), Vector3.back, 1.7f, 11);
        Place(root, "BikeShelter", new Vector3(-13, 0, -6.2f), 90);
        float[] bikes = { -9.8f, -8.7f, -6.4f, -5.3f, -4.1f };
        for (int i = 0; i < bikes.Length; i++)
        {
            var b = Place(root, "Bicycle", new Vector3(-14.4f, 0, bikes[i]), 90 + Rand(-6, 6));
            b.transform.Rotate(0, 0, Rand(-4, 4), Space.Self);
            if (i % 2 == 1) Swap(b, "BikeFrame", "DoorBlue");
            if (i == 4) Swap(b, "BikeFrame", "Metal");
        }
        var fallen = Place(root, "Bicycle", new Vector3(-12.6f, 0.2f, -7.3f), 60);
        fallen.transform.rotation = Quaternion.Euler(0, 60, 78);
        Place(root, "VendingMachine", new Vector3(-13.65f, 0, 1.2f), 90);
        Place(root, "TrashBag", new Vector3(-13.2f, 0, -0.3f), 20);
        Place(root, "TrashBag", new Vector3(-12.6f, 0, 0.35f), 140).transform.localScale = Vector3.one * 0.85f;
        Place(root, "TrashBagBlue", new Vector3(-13.35f, 0, 0.55f), 260);
        Place(root, "CardboardBox", new Vector3(-12.9f, 0, 2.4f), 15);
        var box2 = Place(root, "CardboardBox", new Vector3(-12.8f, 0.4f, 2.35f), 40); box2.transform.localScale = Vector3.one * 0.8f;
        for (int i = 0; i < 14; i++)
        {
            var c = Place(root, "Can", new Vector3(Rand(-12.5f, -5f), 0.033f, Rand(-3f, 5f)), Rand(0, 360));
            c.transform.rotation = Quaternion.Euler(90, Rand(0, 360), 0);
            if (i % 3 == 1) Swap(c, "Red", "Blue"); else if (i % 3 == 2) Swap(c, "Red", "Yellow");
        }

        // Gate and boundary walls (back, "Выход").
        Place(root, "SchoolGate", new Vector3(0, 0, -17), 180);
        var plate = new GameObject("NamePlateText").AddComponent<TextMeshPro>();
        plate.transform.SetParent(root, false);
        plate.font = fontBodyBold; plate.fontSize = 2.2f; plate.color = Hex("#F4EAD2");
        plate.alignment = TextAlignmentOptions.Center; plate.lineSpacing = -28;
        plate.text = "Ш\nК\nО\nЛ\nА\n\n№\n7";
        plate.rectTransform.sizeDelta = new Vector2(0.35f, 1.6f);
        plate.transform.SetPositionAndRotation(new Vector3(-4.3f, 1.5f, -16.48f), Quaternion.Euler(0, 180, 0));
        foreach (float x in new[] { -9.75f, -19.75f, -29.75f, 9.75f, 19.75f, 29.75f })
            Place(root, "BoundaryWall", new Vector3(x, 0, -17), 0);
        foreach (float z in new[] { -12f, -2f, 8f, 18f, 23.5f })
            Place(root, "BoundaryWall", new Vector3(-34.7f, 0, z), 90);
        Place(root, "BoundaryWall", new Vector3(34.7f, 0, -12f), -90);
        foreach (float x in new[] { -24f, -15f, 7.5f, 16f, 25f })
            Decal(root, "Graffiti", new Vector3(x + Rand(-1, 1), 0.2f, -16.86f), Vector3.forward, Rand(1.0f, 1.4f), rng.Next(16));
        Decal(root, "Grime", new Vector3(-12f, 0.75f, -16.865f), Vector3.forward, 1.2f, -1, 1.2f);

        // Trees, planters, bushes.
        Place(root, "Sakura_A", new Vector3(-9.2f, 0, 11.6f), 30);
        Place(root, "Sakura_B", new Vector3(10.4f, 0, 11.2f), 130);
        Place(root, "Sakura_A", new Vector3(-21.5f, 0, -13.2f), 200);
        Place(root, "Sakura_B", new Vector3(-28.5f, 0, -5.5f), 80);
        Place(root, "Sakura_A", new Vector3(22.5f, 0, -13.6f), 300);
        Place(root, "Sakura_B", new Vector3(-9.6f, 0, -14.6f), 10);
        Place(root, "Sakura_A", new Vector3(9.2f, 0, -14.8f), 250).transform.localScale = Vector3.one * 0.85f;
        Place(root, "Sakura_B", new Vector3(-20.5f, 0, 13.5f), 160);
        Place(root, "GreenTree", new Vector3(-28f, 0, 21f), 40);
        Place(root, "GreenTree", new Vector3(-30f, 0, 9.5f), 200).transform.localScale = Vector3.one * 0.9f;
        foreach (float x in new[] { -5.4f, 5.4f }) Place(root, "Planter", new Vector3(x, 0, 13.3f), 180);
        for (float x = -16.2f; x < 16.5f; x += 1.7f)
            if (Mathf.Abs(x) > 4.6f) Place(root, "Bush", new Vector3(x + Rand(-0.2f, 0.2f), 0, 15.35f), Rand(0, 360)).transform.localScale = Vector3.one * Rand(0.8f, 1.1f);
        for (float z = -7f; z < 15f; z += 2.1f)
            if (z < -1.5f || z > 2.5f) Place(root, "Bush", new Vector3(13.35f, 0, z + Rand(-0.3f, 0.3f)), Rand(0, 360)).transform.localScale = Vector3.one * Rand(0.75f, 1.0f);
        var bench = Place(root, "Bench", new Vector3(-6.8f, 0, 8.6f), 150);
        Place(root, "Can", new Vector3(-6.6f, 0.46f, 8.7f), 0);
        Place(root, "TrafficCone", new Vector3(3.4f, 0, -15.4f), 20);
        var cone = Place(root, "TrafficCone", new Vector3(2.4f, 0.16f, -14.6f), 0);
        cone.transform.rotation = Quaternion.Euler(0, 35, 90);
    }

    static void BuildStreet(Transform root)
    {
        float[] poles = { -45f, -27f, -9f, 9f, 27f, 45f };
        foreach (float x in poles) Place(root, "UtilityPole", new Vector3(x, 0.12f, -19.7f), 0);
        var wireMat = mats["Wire"];
        for (int i = 0; i < poles.Length - 1; i++)
            foreach (var (dx, y) in new[] { (-0.7f, 8.74f), (0f, 8.74f), (0.7f, 8.74f), (-0.5f, 7.94f), (0.5f, 7.94f) })
                Wire(root, new Vector3(poles[i] + dx, y, -19.7f), new Vector3(poles[i + 1] + dx, y, -19.7f), 0.55f, wireMat);
        Place(root, "StreetLamp", new Vector3(5.6f, 0.12f, -19.2f), 180);
        string[] houses = { "House_B", "House_A", "House_C", "House_A", "House_B", "House_C", "House_A", "House_B", "House_C", "House_A" };
        float hx = -46f;
        foreach (var h in houses)
        {
            var go = Place(root, h, new Vector3(hx, 0.12f, -30.2f), 0);
            hx += h == "House_B" ? 11.5f : 10f;
        }
        Place(root, "Apartment", new Vector3(-24f, 0, -46f), 0);
        Place(root, "Apartment", new Vector3(22f, 0, -50f), 0);
        Place(root, "Apartment", new Vector3(-6f, 0, 46f), 180);
        foreach (float z in new[] { -6f, 7f, 19f }) Place(root, z > 10 ? "House_C" : "House_A", new Vector3(-42f, 0, z), 90);
        Place(root, "GreenTree", new Vector3(-38f, 0, 1f), 0);
    }

    static void BuildMiniScenes(Transform env)
    {
        var scenes = Group(env, "MiniScenes");
        // Settings wall: Tomura Ryuta smoking against the blank wall.
        var tomura = new GameObject("Tomura_Ryuta").transform;
        tomura.SetParent(scenes, false);
        tomura.SetPositionAndRotation(new Vector3(13.25f, 0, 0.4f), Quaternion.Euler(0, -90 + 18, 0));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Characters/Tomura_Ryuta.fbx"), tomura);
        model.transform.localPosition = Vector3.zero;
        var breathe = model.AddComponent<LoopMotion>(); breathe.bob = new Vector3(0, 0.006f, 0); breathe.frequency = 0.28f;
        var smoke = Smoke(tomura, new Vector3(-0.05f, 1.63f, 0.21f));
        // Gate: a crow on the pillar, looking around.
        var crow = Place(scenes, "Crow", new Vector3(4.25f, 2.75f, -17f), 20);
        var look = crow.AddComponent<LoopMotion>(); look.swayDegrees = 40; look.frequency = 0.09f;
        Place(scenes, "Crow", new Vector3(-26.5f, 8.75f, -19.7f), 160).transform.localScale = Vector3.one * 0.9f;
        // Toilet & vending: buzzing, flickering lights.
        var toilet = GameObject.Find("ToiletBlock");
        var lamp = new GameObject("ToiletLamp").AddComponent<Light>();
        lamp.transform.SetParent(toilet.transform, false);
        lamp.transform.localPosition = B(0, -0.6f, 1.9f);
        lamp.type = LightType.Point; lamp.range = 4; lamp.intensity = 1.5f; lamp.color = Hex("#FFE9A8");
        var flicker = lamp.gameObject.AddComponent<LightFlicker>(); flicker.target = lamp; flicker.glowRenderer = toilet.GetComponent<Renderer>();
        var vm = GameObject.Find("VendingMachine");
        var vlight = new GameObject("VendingGlow").AddComponent<Light>();
        vlight.transform.SetParent(vm.transform, false);
        vlight.transform.localPosition = new Vector3(0, 1.3f, 0.7f);
        vlight.type = LightType.Point; vlight.range = 3; vlight.intensity = 1.2f; vlight.color = Hex("#FFF4D6");
        var vflicker = vlight.gameObject.AddComponent<LightFlicker>(); vflicker.target = vlight; vflicker.glowRenderer = vm.GetComponent<Renderer>();
        vflicker.glowColor = Hex("#FFF4D6"); vflicker.onTime = new Vector2(4f, 9f); vflicker.offTime = new Vector2(0.05f, 0.12f);
        Petals(scenes);
    }

    // ================================================================= lighting, camera, audio

    static Transform BuildLighting(out Volume volume)
    {
        var root = new GameObject("Lighting").transform;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(root, false);
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(34, -18, 0);
        sun.color = Hex("#FFF1DC"); sun.intensity = 1.7f;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.72f;
        RenderSettings.sun = sun;

        var skyPath = MatDir + "Sky_Day1.mat";
        var skyMat = LoadOrCreate(skyPath, Shader.Find("Skybox/Panoramic"));
        skyMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Sky_Day1.png"));
        skyMat.SetFloat("_Mapping", 1); skyMat.SetFloat("_ImageType", 0); skyMat.SetFloat("_Exposure", 1.0f);
        skyMat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f)); skyMat.SetFloat("_Rotation", 0);
        EditorUtility.SetDirty(skyMat);
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#B9DDF4");
        RenderSettings.ambientEquatorColor = Hex("#F1E1D6");
        RenderSettings.ambientGroundColor = Hex("#B7A68F");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Hex("#EDE6EE");
        RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 240;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        DynamicGI.UpdateEnvironment();

        string profilePath = Root + "MainMenu_Day1_Profile.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
        var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(0.95f); bloom.intensity.Override(0.45f); bloom.scatter.Override(0.65f);
        var ca = profile.Add<ColorAdjustments>(true); ca.contrast.Override(6f); ca.saturation.Override(14f); ca.postExposure.Override(0f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.18f); vig.smoothness.Override(0.45f);
        var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(5f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        var volGo = new GameObject("PostFX_Day1");
        volGo.transform.SetParent(root, false);
        volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true; volume.sharedProfile = profile;
        return root;
    }

    static Transform BuildCameraPoses()
    {
        var root = new GameObject("CameraPoses").transform;
        Pose(root, "Home", new Vector3(0, 1.65f, -1.2f), new Vector3(2.6f, 4.6f, 16f));
        Pose(root, "Continue", new Vector3(0, 1.75f, 8.2f), new Vector3(0, 1.9f, 15.5f));
        Pose(root, "Collection", new Vector3(0.8f, 1.65f, -0.4f), new Vector3(-14f, 1.7f, -1.2f));
        Pose(root, "Settings", new Vector3(-0.6f, 1.65f, 0.4f), new Vector3(14f, 2.4f, 4.6f));
        Pose(root, "Exit", new Vector3(0, 1.65f, 0.8f), new Vector3(0, 2.3f, -17f));
        return root;
    }

    static void Pose(Transform root, string name, Vector3 pos, Vector3 target)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        t.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
    }

    static Camera BuildCamera(Transform home)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        cam.fieldOfView = 50; cam.nearClipPlane = 0.1f; cam.farClipPlane = 600;
        go.transform.SetPositionAndRotation(home.position, home.rotation);
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        return cam;
    }

    static MenuAudio BuildAudio()
    {
        var go = new GameObject("MenuAudio");
        var audio = go.AddComponent<MenuAudio>();
        audio.music = go.AddComponent<AudioSource>();
        audio.music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/MainMenu_Music.wav");
        audio.music.loop = true; audio.music.playOnAwake = true; audio.music.spatialBlend = 0;
        audio.sfx = go.AddComponent<AudioSource>();
        audio.sfx.playOnAwake = false;
        audio.hover = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/UI_Hover.ogg");
        audio.click = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/UI_Click.ogg");
        audio.open = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/UI_Open.ogg");
        audio.back = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/UI_Back.ogg");
        var mi = (AudioImporter)AssetImporter.GetAtPath(Root + "Audio/MainMenu_Music.wav");
        var s = mi.defaultSampleSettings;
        if (s.loadType != AudioClipLoadType.Streaming)
        {
            s.loadType = AudioClipLoadType.Streaming; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f;
            mi.defaultSampleSettings = s; mi.SaveAndReimport();
        }
        return audio;
    }

    // ================================================================= UI

    static readonly Color Ink = Hex("#1C1B22"), Paper = Hex("#F2F0EB"), Accent = Hex("#FF5D7A"), Yellow = Hex("#FFD23F");

    static void BuildUI(Camera cam, Transform poses, Volume volume, MenuAudio audio)
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("MenuCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var ui = canvasGo.transform;

        var ctrl = new GameObject("MainMenuController").AddComponent<MainMenuController>();
        ctrl.cam = cam;

        // ---- Home
        var home = Panel(ui, "Home", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(900, 0));
        var col = VStack(home, 14, TextAnchor.MiddleLeft, new RectOffset(0, 0, 0, 0));
        var tagRow = Element(col, "Tag", 46, 380);
        Img(tagRow, "Plate", Ink, plateSprite, true);
        var tagText = Txt(tagRow, "ШКОЛА № 7  ·  ДЕНЬ 1", fontBodyBold, 24, Yellow, TextAlignmentOptions.Center);
        tagText.characterSpacing = 12;
        var title = Txt(Element(col, "Title", 240, 900), "One Funseki,\nSeven Days", fontTitle, 104, Color.white, TextAlignmentOptions.BottomLeft);
        title.fontSharedMaterial = titleMat; title.lineSpacing = -12;
        Element(col, "Gap", 18, 10);
        var bContinue = MenuButton(col, "Продолжить", "сохранение", true, out _);
        var bNew = MenuButton(col, "Новая игра", "вход в школу", false, out _);
        var bCollection = MenuButton(col, "Коллекция", "туалет", false, out _);
        var bSettings = MenuButton(col, "Настройки", "стена", false, out _);
        var bExit = MenuButton(col, "Выход", "ворота", false, out _);
        UnityEventTools.AddPersistentListener(bContinue.onClick, ctrl.Continue);
        UnityEventTools.AddPersistentListener(bNew.onClick, ctrl.NewGame);
        UnityEventTools.AddPersistentListener(bCollection.onClick, ctrl.OpenCollection);
        UnityEventTools.AddPersistentListener(bSettings.onClick, ctrl.OpenSettings);
        UnityEventTools.AddPersistentListener(bExit.onClick, ctrl.OpenExit);
        ctrl.continueButton = bContinue.gameObject;

        // ---- Collection (right side)
        var collection = Panel(ui, "Collection", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(720, 820));
        var cCard = Card(collection, "Коллекция", "Туалет и велопарковка. Здесь хранится всё, что ты успел натворить.");
        var cTabs = Tabs(cCard, new[] { "Ачивки", "Предметы" }, out var cGroup);
        var ach = Page(cCard, "Ачивки"); var items = Page(cCard, "Предметы");
        cGroup.pages = new[] { ach.gameObject, items.gameObject };
        Grid(ach, 12, "Открыто 0 из 24");
        Grid(items, 12, "Найдено 0 из 18");
        var cBack = BackButton(cCard, ctrl);

        // ---- Settings (left side, over the notice board)
        var settings = Panel(ui, "Settings", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(760, 820));
        var sCard = Card(settings, "Настройки", "Глухая стена школы. Объявления, которые никто не читает.");
        var sTabs = Tabs(sCard, new[] { "Изображение", "Аудио", "Управление" }, out var sGroup);
        var pImage = Page(sCard, "Изображение"); var pAudio = Page(sCard, "Аудио"); var pControls = Page(sCard, "Управление");
        sGroup.pages = new[] { pImage.gameObject, pAudio.gameObject, pControls.gameObject };
        var sp = settings.gameObject.AddComponent<SettingsPanel>();
        sp.volume = volume;
        sp.resolutionValue = Stepper(pImage, "Разрешение", sp.StepResolution, out var firstStepper);
        sp.displayValue = Stepper(pImage, "Режим", sp.StepDisplayMode, out _);
        sp.brightness = SliderRow(pImage, "Яркость");
        sp.music = SliderRow(pAudio, "Музыка");
        sp.sfx = SliderRow(pAudio, "Звуки (SFX)");
        sp.vfx = SliderRow(pAudio, "Эффекты (VFX)");
        foreach (var (key, action) in new[] { ("W A S D", "Ходьба"), ("Shift", "Бег"), ("E", "Взаимодействие"), ("Tab", "Сменить героя"), ("Esc", "Пауза / назад") })
            KeyRow(pControls, key, action);
        BackButton(sCard, ctrl);

        // ---- Exit dialog
        var exit = Panel(ui, "Exit", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(900, 420));
        var eCol = VStack(exit, 22, TextAnchor.LowerCenter, new RectOffset(0, 0, 0, 0));
        var q = Txt(Element(eCol, "Question", 170, 900), "Сосал?", fontTitle, 150, Color.white, TextAlignmentOptions.Center);
        q.fontSharedMaterial = titleMat;
        var eRow = HStack(Element(eCol, "Answers", 76, 900).transform, 40, TextAnchor.MiddleCenter);
        var yes = MenuButton(eRow, "Да", "на рабочий стол", false, out _, 360);
        var no = MenuButton(eRow, "Нет", "в меню", true, out _, 360);
        UnityEventTools.AddPersistentListener(yes.onClick, ctrl.QuitGame);
        UnityEventTools.AddPersistentListener(no.onClick, ctrl.Back);

        // ---- Hints and fade
        var hint = Txt(Anchored(ui, "Hint", new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 46), new Vector2(900, 40)),
            "<b>Enter</b> — выбрать     <b>Esc</b> — назад", fontBody, 22, new Color(1, 1, 1, 0.92f), TextAlignmentOptions.Left);
        hint.fontSharedMaterial = titleMat;
        var ver = Txt(Anchored(ui, "Version", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 46), new Vector2(500, 40)),
            "ранняя версия · 0.1", fontBody, 20, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.Right);
        ver.fontSharedMaterial = titleMat;
        var fadeImg = Img(ui, "Fade", Color.black, null, true);
        var fade = fadeImg.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        ctrl.fade = fade;

        ctrl.views = new[]
        {
            View(MainMenuController.Section.Home, poses, "Home", home, bNew, 50),
            View(MainMenuController.Section.Continue, poses, "Continue", null, null, 40),
            View(MainMenuController.Section.Collection, poses, "Collection", collection, cTabs[0], 48),
            View(MainMenuController.Section.Settings, poses, "Settings", settings, sTabs[0], 50),
            View(MainMenuController.Section.Exit, poses, "Exit", exit, no, 46),
        };
    }

    static MainMenuController.View View(MainMenuController.Section s, Transform poses, string pose, RectTransform panel, Selectable first, float fov)
    {
        CanvasGroup g = null;
        if (panel) g = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        return new MainMenuController.View { section = s, pose = poses.Find(pose), panel = g, firstSelected = first, fov = fov };
    }

    // ---- UI building blocks

    static RectTransform Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Rect(parent, name);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        rt.gameObject.AddComponent<CanvasGroup>();
        return rt;
    }

    static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        => Panel2(parent, name, anchor, pivot, pos, size);

    static RectTransform Panel2(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Rect(parent, name);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Card(RectTransform panel, string title, string subtitle)
    {
        Img(panel, "Shadow", new Color(0.11f, 0.1f, 0.14f, 0.35f), whiteSprite, true).rectTransform.offsetMin = new Vector2(12, -12);
        var bg = Img(panel, "Background", Paper, whiteSprite, true);
        bg.rectTransform.offsetMax = new Vector2(-12, 0);
        var bar = Img(panel, "TopBar", Yellow, whiteSprite, false);
        bar.rectTransform.anchorMin = new Vector2(0, 1); bar.rectTransform.anchorMax = new Vector2(1, 1);
        bar.rectTransform.pivot = new Vector2(0.5f, 1); bar.rectTransform.sizeDelta = new Vector2(-12, 10); bar.rectTransform.anchoredPosition = new Vector2(-6, 0);
        var content = Rect(panel, "Content");
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = new Vector2(44, 36); content.offsetMax = new Vector2(-56, -44);
        var col = VStack(content, 14, TextAnchor.UpperLeft, new RectOffset(0, 0, 0, 0));
        Txt(Element(col, "Title", 64, 600), title, fontTitle, 48, Ink, TextAlignmentOptions.Left);
        var sub = Txt(Element(col, "Subtitle", 54, 640), subtitle, fontBody, 21, new Color(0.33f, 0.31f, 0.36f), TextAlignmentOptions.TopLeft);
        sub.textWrappingMode = TextWrappingModes.Normal;
        return col;
    }

    static Button[] Tabs(RectTransform col, string[] names, out TabGroup group)
    {
        var row = HStack(Element(col, "Tabs", 52, 640).transform, 10, TextAnchor.MiddleLeft);
        group = col.parent.gameObject.AddComponent<TabGroup>(); // the panel that owns this card
        var buttons = new Button[names.Length];
        var tabs = new MenuTab[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var b = Element(row, names[i], 52, 200);
            var bg = Img(b, "Bg", Accent, plateSprite, true);
            var label = Txt(b, names[i], fontBodyBold, 22, Color.white, TextAlignmentOptions.Center);
            var outline = Img(b, "Outline", new Color(0.11f, 0.1f, 0.14f, 0.15f), plateSprite, true);
            outline.raycastTarget = false; outline.transform.SetAsFirstSibling();
            var btn = b.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors; colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f); colors.selectedColor = new Color(0.9f, 0.9f, 0.9f); btn.colors = colors;
            var tab = b.gameObject.AddComponent<MenuTab>(); tab.background = bg; tab.label = label;
            UnityEventTools.AddIntPersistentListener(btn.onClick, group.ShowWithSound, i);
            buttons[i] = btn; tabs[i] = tab;
        }
        group.tabs = tabs;
        Element(col, "Rule", 2, 640).gameObject.AddComponent<Image>().color = new Color(0.11f, 0.1f, 0.14f, 0.15f);
        return buttons;
    }

    static RectTransform Page(RectTransform col, string name)
    {
        var page = Element(col, "Page_" + name, 470, 640);
        var inner = VStack(page, 16, TextAnchor.UpperLeft, new RectOffset(0, 0, 12, 0));
        return inner;
    }

    static void Grid(RectTransform page, int count, string progress)
    {
        Txt(Element(page, "Progress", 34, 640), progress, fontBodyBold, 24, Ink, TextAlignmentOptions.Left);
        var gridRt = Element(page, "Grid", 390, 640);
        var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(146, 118); grid.spacing = new Vector2(12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        for (int i = 0; i < count; i++)
        {
            var cell = Rect(gridRt, "Slot" + i);
            Img(cell, "Bg", new Color(0.11f, 0.1f, 0.14f, 0.9f), whiteSprite, true);
            var q = Txt(cell, "?", fontTitle, 46, new Color(1, 1, 1, 0.25f), TextAlignmentOptions.Center);
            q.rectTransform.offsetMin = new Vector2(0, 26);
            var cap = Txt(cell, "???", fontBodyBold, 18, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Bottom);
            cap.rectTransform.offsetMin = new Vector2(0, 12);
        }
    }

    static TMP_Text Stepper(RectTransform page, string label, UnityEngine.Events.UnityAction<int> step, out Button first)
    {
        var row = HStack(Element(page, label, 64, 640).transform, 10, TextAnchor.MiddleLeft);
        Txt(Element(row, "Label", 64, 230), label, fontBodyBold, 26, Ink, TextAlignmentOptions.Left);
        first = ArrowButton(row, "‹", step, -1);
        var valBox = Element(row, "Value", 56, 270);
        Img(valBox, "Bg", Color.white, plateSprite, true);
        var val = Txt(valBox, "—", fontBodyBold, 24, Ink, TextAlignmentOptions.Center);
        ArrowButton(row, "›", step, 1);
        return val;
    }

    static Button ArrowButton(RectTransform row, string glyph, UnityEngine.Events.UnityAction<int> step, int dir)
    {
        var b = Element(row, glyph == "‹" ? "Prev" : "Next", 56, 56);
        var bg = Img(b, "Bg", Ink, plateSprite, true);
        Txt(b, glyph, fontTitle, 34, Yellow, TextAlignmentOptions.Center).rectTransform.offsetMin = new Vector2(0, 4);
        var btn = b.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors; colors.highlightedColor = Accent; colors.selectedColor = Accent; colors.pressedColor = Yellow; btn.colors = colors;
        UnityEventTools.AddIntPersistentListener(btn.onClick, step, dir);
        return btn;
    }

    static Slider SliderRow(RectTransform page, string label)
    {
        var row = HStack(Element(page, label, 64, 640).transform, 10, TextAnchor.MiddleLeft);
        Txt(Element(row, "Label", 64, 230), label, fontBodyBold, 26, Ink, TextAlignmentOptions.Left);
        var sRt = Element(row, "Slider", 48, 380);
        var slider = sRt.gameObject.AddComponent<Slider>();
        var track = Img(sRt, "Track", new Color(0.11f, 0.1f, 0.14f, 0.18f), whiteSprite, false);
        track.rectTransform.anchorMin = new Vector2(0, 0.5f); track.rectTransform.anchorMax = new Vector2(1, 0.5f); track.rectTransform.sizeDelta = new Vector2(0, 12);
        var fillArea = Rect(sRt, "FillArea");
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.sizeDelta = new Vector2(-20, 12);
        var fill = Img(fillArea, "Fill", Accent, whiteSprite, false);
        fill.rectTransform.sizeDelta = new Vector2(10, 0);
        var handleArea = Rect(sRt, "HandleArea");
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.sizeDelta = new Vector2(-20, 0);
        var handle = Img(handleArea, "Handle", Ink, plateSprite, false);
        handle.rectTransform.sizeDelta = new Vector2(30, 44);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
        var colors = slider.colors; colors.highlightedColor = Accent; colors.selectedColor = Accent; colors.pressedColor = Yellow; slider.colors = colors;
        return slider;
    }

    static void KeyRow(RectTransform page, string key, string action)
    {
        var row = HStack(Element(page, key, 56, 640).transform, 18, TextAnchor.MiddleLeft);
        var k = Element(row, "Key", 50, 200);
        Img(k, "Bg", Ink, plateSprite, true);
        Txt(k, key, fontBodyBold, 22, Yellow, TextAlignmentOptions.Center);
        Txt(Element(row, "Action", 50, 380), action, fontBody, 24, Ink, TextAlignmentOptions.Left);
    }

    static Button BackButton(RectTransform col, MainMenuController ctrl)
    {
        Element(col, "Spacer", 6, 10);
        var b = MenuButton(col, "Назад", "Esc", false, out _, 300);
        UnityEventTools.AddPersistentListener(b.onClick, ctrl.Back);
        return b;
    }

    static Button MenuButton(RectTransform parent, string label, string hint, bool primary, out MenuButtonFx fx, float width = 460)
    {
        var root = Element(parent, label, 68, width + 30);
        var plate = Rect(root, "Plate");
        plate.anchorMin = new Vector2(0, 0); plate.anchorMax = new Vector2(0, 1); plate.pivot = new Vector2(0, 0.5f);
        plate.sizeDelta = new Vector2(width, 0); plate.anchoredPosition = Vector2.zero;
        var shadow = Img(plate, "Shadow", new Color(0.11f, 0.1f, 0.14f, 0.35f), plateSprite, true);
        shadow.rectTransform.offsetMin = new Vector2(6, -6); shadow.rectTransform.offsetMax = new Vector2(6, -6);
        var bg = Img(plate, "Bg", primary ? Yellow : Color.white, plateSprite, true);
        var accent = Img(plate, "Accent", primary ? Ink : Accent, plateSprite, false);
        accent.rectTransform.anchorMin = new Vector2(0, 0); accent.rectTransform.anchorMax = new Vector2(0, 1);
        accent.rectTransform.pivot = new Vector2(0, 0.5f); accent.rectTransform.sizeDelta = new Vector2(34, 0);
        var text = Txt(plate, label, fontTitle, 30, Ink, TextAlignmentOptions.Left);
        text.rectTransform.offsetMin = new Vector2(46, 2);
        var hintT = Txt(plate, hint, fontBody, 18, new Color(Ink.r, Ink.g, Ink.b, 0.6f), TextAlignmentOptions.Right);
        hintT.rectTransform.offsetMax = new Vector2(-34, 0);
        var btn = root.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;
        fx = root.gameObject.AddComponent<MenuButtonFx>();
        fx.plate = plate; fx.background = bg; fx.label = text; fx.hint = hintT;
        fx.normalBg = primary ? Yellow : Color.white;
        fx.hotBg = Accent;
        return btn;
    }

    static RectTransform VStack(RectTransform parent, float spacing, TextAnchor align, RectOffset pad)
    {
        var v = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.childAlignment = align; v.padding = pad;
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = false;
        return parent;
    }

    static RectTransform HStack(Transform parent, float spacing, TextAnchor align)
    {
        var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childAlignment = align;
        h.childControlHeight = true; h.childControlWidth = true; h.childForceExpandHeight = false; h.childForceExpandWidth = false;
        return (RectTransform)parent;
    }

    static RectTransform Element(Transform parent, string name, float height, float width)
    {
        var rt = Rect(parent, name);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height; le.minHeight = height;
        le.preferredWidth = width; le.minWidth = width;
        return rt;
    }

    static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Color color, Sprite sprite, bool stretch)
    {
        var rt = Rect(parent, name);
        if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color;
        if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    static TextMeshProUGUI Txt(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var rt = Rect(parent, "Text");
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        return t;
    }

    // ================================================================= helpers

    static Vector3 B(float x, float y, float z) => new Vector3(-x, z, -y); // Blender-local -> Unity-local

    static GameObject Place(Transform parent, string model, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
        if (!prefab) { Debug.LogError("Missing model " + model); return new GameObject(model); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        return go;
    }

    static void Swap(GameObject go, string from, string to)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var sm = r.sharedMaterials;
            for (int i = 0; i < sm.Length; i++) if (sm[i] == mats[from]) sm[i] = mats[to];
            r.sharedMaterials = sm;
        }
    }

    static GameObject Slab(Transform parent, string name, Vector3 center, Vector3 size, string mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        return go;
    }

    static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, string mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        return go;
    }

    // Flat quad facing `normal`; cell >= 0 picks a cell from the 4x4 graffiti atlas.
    static void Decal(Transform parent, string mat, Vector3 pos, Vector3 normal, float size, int cell, float aspect = 0.8f)
    {
        var m = new Mesh { name = "Decal" };
        float w = size / 2, h = size * aspect / 2;
        var uv = cell >= 0 ? new UnityEngine.Rect((cell % 4) * 0.25f, (cell / 4) * 0.25f, 0.25f, 0.25f) : new UnityEngine.Rect(0, 0, 1, 1);
        m.vertices = new[] { new Vector3(-w, -h), new Vector3(w, -h), new Vector3(-w, h), new Vector3(w, h) };
        m.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax) };
        m.triangles = new[] { 0, 2, 3, 0, 3, 1 };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.RecalculateTangents(); m.RecalculateBounds();
        var go = new GameObject(mat + "Decal");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Rand(-4, 4)));
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mats[mat];
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void Wire(Transform parent, Vector3 a, Vector3 b, float sag, Material mat)
    {
        var lr = new GameObject("Wire").AddComponent<LineRenderer>();
        lr.transform.SetParent(parent, false);
        lr.useWorldSpace = true; lr.positionCount = 14; lr.widthMultiplier = 0.03f;
        lr.sharedMaterial = mat; lr.shadowCastingMode = ShadowCastingMode.Off;
        for (int i = 0; i < 14; i++)
        {
            float t = i / 13f;
            lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t));
        }
    }

    static ParticleSystem Smoke(Transform parent, Vector3 local)
    {
        var go = new GameObject("CigaretteSmoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 3.2f; main.startSpeed = 0.12f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new Color(0.95f, 0.95f, 0.97f, 0.5f); main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60; main.gravityModifier = -0.03f; main.prewarm = true;
        var em = ps.emission; em.rateOverTime = 6;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6; sh.radius = 0.005f;
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3.2f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.08f; noise.frequency = 0.6f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("SmokeParticle", softDot.texture, Color.white);
        return ps;
    }

    static void Petals(Transform parent)
    {
        var go = new GameObject("SakuraPetals");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0, 9, 2);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 14; main.startSpeed = 0.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new ParticleSystem.MinMaxGradient(Hex("#FBC9DA"), Hex("#F39AB9"));
        main.gravityModifier = 0.012f; main.maxParticles = 900; main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 55;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(56, 2, 40);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.25f); vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.1f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.25f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("PetalParticle", PetalTex(), Color.white);
    }

    static Texture2D PetalTex()
    {
        string path = TexDir + "Petal.png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float u = (x - 31.5f) / 32f, v = (y - 31.5f) / 32f;
                    float d = (u * u) / 0.35f + (v * v) / 0.9f;
                    float notch = v > 0.55f && Mathf.Abs(u) < 0.12f ? 1 : 0;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - d) * 6f) * (1 - notch)));
                }
            SaveTexture(t, "Petal", ti => ti.alphaIsTransparency = true);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ParticleMat(string name, Texture tex, Color color)
    {
        var m = LoadOrCreate(MatDir + name + ".mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", color);
        Transparent(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static void SetStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (!t.GetComponent<LoopMotion>() && !t.GetComponentInParent<LoopMotion>()) t.gameObject.isStatic = true;
    }

    static Material LoadOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static void SaveTexture(Texture2D t, string name, System.Action<TextureImporter> configure)
    {
        t.Apply();
        string path = TexDir + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        configure?.Invoke(ti);
        ti.SaveAndReimport();
    }

    static Sprite SaveSprite(Texture2D t, string name, Vector4 border)
    {
        t.Apply();
        string path = Root + "UI/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.spriteBorder = border;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Color Hex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        c.a = alpha;
        return c;
    }

    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
