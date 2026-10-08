using System.IO;
using Funseki.Core;
using Funseki.Interaction;
using Funseki.Inventory;
using Funseki.Save;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Interaction > Place slice interactables in Slice_Day1:
    // the world objects of the slice (GDD 5.9 and the Notion item specs) as grey boxes under the root "Interactables",
    // plus the marker pickup in the courtyard (PE lesson area). The spec additions (more taps, 7 posters + 4 paintings
    // with drawing, the shower, the paint pickup) sit in the child "Spec"; "Add spec items" rebuilds only that child
    // and the corridor poster, keeping hand-moved objects of the root.
    // Positions are School_Greybox plan meters (x, -y) from SchoolLayout.
    // Data assets in Data/Interaction/Interactables are created once and kept (designer edits survive; the spec
    // changes of the old five assets are applied once, see ApplySpecToOldData); "Place slice interactables" rebuilds
    // the whole "Interactables" root. Other roots of Slice_Day1 are not touched.
    // Also puts InventoryService, InventoryPanel and SaveService under [Bootstrap] if they are missing.
    public static class InteractablesSetup
    {
        const string DataDir = "Assets/_Project/Data/Interaction/Interactables";
        const string SaveDir = "Assets/_Project/Data/Save";
        const string ArtDir = "Assets/_Project/Art/Placeholders/Interactables";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string MarkerPath = "Assets/_Project/Data/Inventory/Items/Item_Marker.asset";
        const string PaintPath = "Assets/_Project/Data/Inventory/Items/Item_Paint.asset";
        const string InventorySettingsPath = "Assets/_Project/Data/Inventory/InventorySettings.asset";
        const string SchoolMapPath = "Assets/_Project/School/Docs/School_TopView_F1.png";
        const string RootName = "Interactables";
        const string SpecName = "Spec";

        // Plan of School_Greybox floor 1: world x = plan x, world z = -plan y; yaw 180 faces plan south (-Z).
        static readonly Vector3 TapPos = new(53f, 0f, -30.25f);          // Туалет М, over the first sink by the north wall
        static readonly Vector3 VendingCorridorPos = new(12f, 0f, -37.55f); // corridor with shoe lockers, north wall
        static readonly Vector3 VendingYardPos = new(16f, 0f, -29.55f);  // courtyard, south wall, next to the PE area
        static readonly Vector3 LockerPos = new(19.6f, 0f, -37.3f);      // corridor at the entrance, after the shoe lockers
        static readonly Vector3 PosterPos = new(33f, 0f, -37.03f);       // corridor at the entrance, wall of Анатомия, before its shoe lockers
        static readonly Vector3 RackPos = new(28.2f, 0f, -41.75f);       // by the main entrance doors, inside
        static readonly Vector3 MarkerPos = new(21f, 0.06f, -23.3f);     // courtyard, PE lesson area, by the west bench

        // Spec additions (the "Spec" child). Sinks of the toilets are on their north walls (SchoolFurnisher: door west).
        static readonly (string id, string name, Vector3 pos)[] ExtraTaps =
        {
            ("tap_wc_m_2", "WaterTap_WC_M_2", new Vector3(53.8f, 0f, -30.25f)),   // Туалет М, second sink
            ("tap_wc_f", "WaterTap_WC_F", new Vector3(53f, 0f, -22.25f)),         // Туалет Ж, first sink
            ("tap_wc_f_2", "WaterTap_WC_F_2", new Vector3(53.8f, 0f, -22.25f)),   // Туалет Ж, second sink
        };
        // Spec: 7 posters and 4 paintings around the school. Corridor walls between the classroom doors
        // (north rooms: doors at x = room + 5; south rooms: shop 11, shop_2 20, anatomy 30, literature 39).
        static readonly (string id, string name, Vector3 pos, float yaw)[] ExtraPosters =
        {
            ("poster_n_1", "Poster_North_1", new Vector3(15f, 0f, -8.03f), 180f),  // north corridor, wall of the heroes' class
            ("poster_n_2", "Poster_North_2", new Vector3(23f, 0f, -8.03f), 180f),  // wall of Геометрия
            ("poster_n_3", "Poster_North_3", new Vector3(31f, 0f, -8.03f), 180f),  // wall of Музыка
            ("poster_n_4", "Poster_North_4", new Vector3(39f, 0f, -8.03f), 180f),  // wall of Физика
            ("poster_s_1", "Poster_South_1", new Vector3(8f, 0f, -37.03f), 180f),  // shoe-locker corridor, wall of Труд
            ("poster_s_2", "Poster_South_2", new Vector3(16.5f, 0f, -37.03f), 180f), // wall of the shop store room
        };
        static readonly (string id, string name, Vector3 pos, float yaw)[] Paintings =
        {
            ("painting_n", "Painting_North", new Vector3(4.5f, 0f, -8.03f), 180f),  // north corridor, wall of the utility room
            ("painting_w", "Painting_West", new Vector3(5.97f, 0f, -33f), 270f),    // west corridor, wall of Труд
            ("painting_e", "Painting_East", new Vector3(46.03f, 0f, -33.5f), 90f),  // east corridor, wall of Литература
            ("painting_canteen", "Painting_Canteen", new Vector3(51.97f, 0f, -18f), 270f), // east corridor, wall of the canteen
        };
        static readonly Vector3 ShowerPos = new(55f, 0f, -37.95f);       // Туалет М, south wall (no dorm floor in the slice yet)
        static readonly Vector3 PaintPos = new(9.2f, 0.11f, -8.45f);     // north corridor, by the door of Рисование

        [MenuItem("Tools/Funseki/Interaction/Place slice interactables in Slice_Day1")]
        public static void Build()
        {
            var a = Prepare();
            var slice = OpenSlice(out var active, out bool opened);

            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);
            var root = new GameObject(RootName).transform;

            BuildTap(root, a, "tap_wc_m", "WaterTap_WC_M", TapPos);
            BuildVending(root, a, "vending_corridor", "VendingMachine_Corridor", VendingCorridorPos, 180f);
            BuildVending(root, a, "vending_courtyard", "VendingMachine_Courtyard", VendingYardPos, 0f);
            BuildLocker(root, a);
            BuildPoster(root, a, a.poster, "poster_corridor", "Poster_Corridor", PosterPos, 180f, false);
            BuildRack(root, a);
            BuildMarker(root, a);
            BuildSpec(root, a);

            CloseSlice(slice, active, opened);
            Debug.Log("[InteractablesSetup] Slice_Day1: taps, 2 vending machines, Рюта's locker, 7 posters, 4 paintings, booklet rack, " +
                      "shower, marker and paint placed under 'Interactables'.");
        }

        // Adds the spec objects without touching the rest of the root (hand-moved machines, lockers...).
        // The corridor poster is rebuilt in its current place, because drawing needs a close-up camera on it.
        [MenuItem("Tools/Funseki/Interaction/Add spec items to Slice_Day1 (keeps the rest)")]
        public static void AddSpecItems()
        {
            var a = Prepare();
            var slice = OpenSlice(out var active, out bool opened);

            Transform root = null;
            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) root = go.transform;
            if (root == null) root = new GameObject(RootName).transform;

            var old = root.Find("Poster_Corridor");
            Vector3 posterPos = old != null ? old.position : PosterPos;
            float posterYaw = old != null ? old.eulerAngles.y : 180f;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            BuildPoster(root, a, a.poster, "poster_corridor", "Poster_Corridor", posterPos, posterYaw, false);

            BuildSpec(root, a);
            CloseSlice(slice, active, opened);
            Debug.Log("[InteractablesSetup] Slice_Day1: spec items (3 taps, 6 posters, 4 paintings, shower, paint) placed under 'Interactables/Spec'; " +
                      "the corridor poster got its drawing camera.");
        }

        static Assets Prepare()
        {
            foreach (var dir in new[] { DataDir, SaveDir, ArtDir }) Directory.CreateDirectory(dir);
            var a = EnsureAssets();
            AddServicesToBootstrap();
            return a;
        }

        static Scene OpenSlice(out Scene active, out bool opened)
        {
            active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            return slice;
        }

        static void CloseSlice(Scene slice, Scene active, bool opened)
        {
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }

        static void BuildSpec(Transform root, Assets a)
        {
            var old = root.Find(SpecName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var spec = new GameObject(SpecName).transform;
            spec.SetParent(root, false);

            foreach (var t in ExtraTaps) BuildTap(spec, a, t.id, t.name, t.pos);
            foreach (var p in ExtraPosters) BuildPoster(spec, a, a.poster, p.id, p.name, p.pos, p.yaw, false);
            foreach (var p in Paintings) BuildPoster(spec, a, a.painting, p.id, p.name, p.pos, p.yaw, true);
            BuildShower(spec, a);
            BuildPaint(spec, a);
        }

        // ---------------------------------------------------------------- data

        class Assets
        {
            public WaterTapData tap;
            public VendingMachineData vending;
            public LockerData locker;
            public PosterData poster, painting;
            public ShowerData shower;
            public BookletData booklet;
            public ItemData marker, paint;
            public InputActionAsset input;
            public Material water, metal, porcelain, machine, machineScreen, slot, lockerBody, lockerDoor, frame, paper, wood,
                photo, shoe, book, bento, markerMat, canvas, tile, tray, paintCan;
        }

        static Assets EnsureAssets()
        {
            var a = new Assets();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            a.marker = AssetDatabase.LoadAssetAtPath<ItemData>(MarkerPath);
            a.input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerSliceSetup.InputPath);

            var posterClean = PosterTexture("T_Poster_Clean", 0);
            var drawn = new[] { PosterTexture("T_Poster_Drawn_1", 1), PosterTexture("T_Poster_Drawn_2", 2), PosterTexture("T_Poster_Drawn_3", 3) };
            var cover = CoverTexture("T_Booklet_Cover");
            var schoolMap = AssetDatabase.LoadAssetAtPath<Texture2D>(SchoolMapPath);
            var paintingClean = PaintingTexture("T_Painting_Clean", 0);
            var paintingDrawn = new[] { PaintingTexture("T_Painting_Drawn_1", 1), PaintingTexture("T_Painting_Drawn_2", 2), PaintingTexture("T_Painting_Drawn_3", 3) };
            a.paint = PaintItem();

            a.tap = Data<WaterTapData>("Interactable_WaterTap", d =>
            {
                d.firstLines = new[] { "Дерьмо." };
                d.repeatLines = new string[0];
            });
            a.vending = Data<VendingMachineData>("Interactable_VendingMachine", d =>
            {
                d.prompt = "Купить газировку";
                d.firstLines = new[] { "О, работает." };
                d.repeatLines = new string[0];
            });
            a.locker = Data<LockerData>("Interactable_RyutaLocker", d =>
            {
                d.prompt = "Открыть шкафчик Рюты";
                d.firstLines = new[] { "Ну и бардак." };
                d.repeatLines = new[] { "Ну и бардак." };
                d.font = font;
            });
            a.poster = Data<PosterData>("Interactable_Poster", d =>
            {
                d.prompt = "Рассмотреть плакат";
                d.firstLines = new[] { "«Чистые руки — светлое будущее». Спорно." };
                d.repeatLines = new[] { "Висит. Смотрит." };
                d.heroLines.Add(new HeroLines { hero = HeroId.Rei, first = new[] { "Шрифт — Arial. Это преступление." } });
                d.heroLines.Add(new HeroLines { hero = HeroId.Kaito, first = new[] { "Голос говорит, тут не хватает усов." } });
                d.cleanTexture = posterClean;
                d.drawnTextures = drawn;
                d.drawItem = a.marker;
            });
            a.booklet = Data<BookletData>("Interactable_Booklet", d =>
            {
                d.prompt = "Взять буклет";
                d.firstLines = new[] { "Полная залупа." };
                d.repeatLines = new[] { "Полная залупа." };
                d.font = font;
                d.pages = new[]
                {
                    new BookletData.Page
                    {
                        title = "Добро пожаловать в «Фунсэки»!", image = cover,
                        text = "Школа основана в 1974 году у подножия спящего вулкана. «Фунсэки» означает «вулканические бомбы». " +
                               "Других значений у этого слова нет.",
                    },
                    new BookletData.Page
                    {
                        title = "Первый этаж", image = schoolMap,
                        text = "Вы находитесь у главного входа. Лифт ездит с первого этажа на первый. " +
                               "Лестница на третий этаж ведёт к совещанию. Не мешайте совещанию.",
                    },
                    new BookletData.Page
                    {
                        title = "Правила школы",
                        text = "1. Сменная обувь обязательна.\n2. Бегать по коридорам запрещено.\n3. Шуметь запрещено.\n" +
                               "4. Задавать вопросы о третьем этаже запрещено.\n5. Если вас вызвали на совещание, это для вашего же блага.",
                    },
                };
            });
            a.painting = Data<PosterData>("Interactable_Painting", d =>
            {
                d.prompt = "Рассмотреть картину";
                d.firstLines = new[] { "Фудзи. Как у всех." };
                d.repeatLines = new[] { "Гора как гора." };
                d.heroLines.Add(new HeroLines { hero = HeroId.Rei, first = new[] { "Подпись «Директор, 1974». Многое объясняет." } });
                d.heroLines.Add(new HeroLines { hero = HeroId.Kaito, first = new[] { "Голос говорит, Фудзи грустит." } });
                d.font = font;
                d.cleanTexture = paintingClean;
                d.drawnTextures = paintingDrawn;
                d.drawItem = a.marker;
                d.drawLines = new[] { "Фудзи стало веселее.", "Теперь тут есть сюжет.", "Музей оторвёт с руками." };
                d.heroDrawLines.Add(new HeroLines { hero = HeroId.Ryuta, first = new[] { "Голоса в голове аплодируют." } });
            });
            a.shower = Data<ShowerData>("Interactable_Shower", d =>
            {
                d.prompt = "Включить душ";
                d.firstLines = new[] { "Вода ледяная. Конечно." };
                d.repeatLines = new string[0];
                d.paintItem = a.paint;
            });
            ApplySpecToOldData(a, font);

            a.water = Mat("M_Water", new Color(0.65f, 0.85f, 1f, 0.55f), transparent: true);
            a.metal = Mat("M_TapMetal", new Color(0.75f, 0.77f, 0.8f), metallic: 0.8f);
            a.porcelain = Mat("M_Porcelain", new Color(0.92f, 0.93f, 0.95f));
            a.machine = Mat("M_VendingBody", new Color(0.8f, 0.12f, 0.15f));
            a.machineScreen = Mat("M_VendingFront", new Color(0.9f, 0.95f, 1f), emission: new Color(0.6f, 0.7f, 0.8f));
            a.slot = Mat("M_VendingSlot", new Color(0.08f, 0.08f, 0.08f));
            a.lockerBody = Mat("M_Locker", new Color(0.45f, 0.55f, 0.62f));
            a.lockerDoor = Mat("M_LockerDoor_Ryuta", new Color(0.32f, 0.45f, 0.6f));
            a.frame = Mat("M_PosterFrame", new Color(0.2f, 0.18f, 0.16f));
            a.paper = Mat("M_Poster", Color.white, texture: posterClean);
            a.wood = Mat("M_RackWood", new Color(0.55f, 0.4f, 0.28f));
            a.photo = Mat("M_Detail_Photo", new Color(0.95f, 0.8f, 0.85f));
            a.shoe = Mat("M_Detail_Shoe", new Color(0.95f, 0.95f, 0.95f));
            a.book = Mat("M_Detail_Book", new Color(0.2f, 0.55f, 0.3f));
            a.bento = Mat("M_Detail_Bento", new Color(0.55f, 0.6f, 0.2f));
            a.markerMat = Mat("M_Pickup_Marker", new Color(0.85f, 0.15f, 0.15f));
            a.canvas = Mat("M_Painting", Color.white, texture: paintingClean);
            a.tile = Mat("M_ShowerTile", new Color(0.78f, 0.86f, 0.88f));
            a.tray = Mat("M_ShowerTray", new Color(0.9f, 0.9f, 0.92f));
            a.paintCan = Mat("M_Pickup_Paint", new Color(0.95f, 0.2f, 0.6f));

            Asset<SaveSettings>($"{SaveDir}/SaveSettings.asset", _ => { });
            AssetDatabase.SaveAssets();
            return a;
        }

        // The Notion specs changed some of the first five objects. Applied once: each block runs only while its asset
        // still has the old value it replaces, so later designer edits are kept.
        static void ApplySpecToOldData(Assets a, TMP_FontAsset font)
        {
            // «Стелаж с журналами»: only Рюта, once, on day 1; «Ну и залупа» after closing.
            if (a.booklet != null && (a.booklet.allowedHeroes == null || a.booklet.allowedHeroes.Length == 0))
            {
                a.booklet.allowedHeroes = new[] { HeroId.Ryuta };
                a.booklet.repeat = InteractRepeat.Once;
                a.booklet.onlyOnDays = new[] { 1 };
                a.booklet.prompt = "Почитать журнал";
                a.booklet.firstLines = new[] { "Ну и залупа." };
                a.booklet.repeatLines = new string[0];
                EditorUtility.SetDirty(a.booklet);
            }
            // «Кранчик с водой»: own lines of Рэй and Кайто.
            if (a.tap != null && a.tap.heroLines.Count == 0)
            {
                a.tap.heroLines.Add(new HeroLines { hero = HeroId.Rei, first = new[] { "Отвратительно." } });
                a.tap.heroLines.Add(new HeroLines { hero = HeroId.Kaito, first = new[] { "Голос: «Ямми»." } });
                EditorUtility.SetDirty(a.tap);
            }
            // «Плакаты и картины»: the close-up screen font and Рюта's «голоса в голове».
            if (a.poster != null && a.poster.heroDrawLines.Count == 0)
            {
                if (a.poster.font == null) a.poster.font = font;
                a.poster.heroDrawLines.Add(new HeroLines { hero = HeroId.Ryuta, first = new[] { "Голоса в голове одобряют." } });
                EditorUtility.SetDirty(a.poster);
            }
        }

        // «Краска» for the shower; added to InventorySettings.allItems so the save can find it by id.
        static ItemData PaintItem()
        {
            var item = Asset<ItemData>(PaintPath, d =>
            {
                d.id = "paint";
                d.displayName = "Краска";
                d.description = "Банка розовой краски для плакатов. Почти полная.";
            });
            var settings = AssetDatabase.LoadAssetAtPath<InventorySettings>(InventorySettingsPath);
            if (settings != null && (settings.allItems == null || System.Array.IndexOf(settings.allItems, item) < 0))
            {
                var list = new System.Collections.Generic.List<ItemData>(settings.allItems ?? new ItemData[0]) { item };
                settings.allItems = list.ToArray();
                EditorUtility.SetDirty(settings);
            }
            return item;
        }

        static T Data<T>(string name, System.Action<T> init) where T : ScriptableObject =>
            Asset($"{DataDir}/{name}.asset", init);

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Mat(string name, Color color, bool transparent = false, float metallic = 0f,
            Color? emission = null, Texture texture = null)
        {
            string path = $"{ArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- placeholder textures

        // 0: clean poster (a smiling «good student» and a slogan bar); 1-3: the same poster defaced with the marker.
        static Texture2D PosterTexture(string name, int variant)
        {
            const int w = 256, h = 384;
            var px = new Color32[w * h];
            Fill(px, w, 0, 0, w, h, new Color32(240, 232, 205, 255));
            Fill(px, w, 12, h - 82, w - 24, 70, new Color32(40, 90, 160, 255));        // slogan bar (y grows upward)
            Fill(px, w, 30, h - 40, 196, 10, new Color32(235, 235, 235, 255));
            Fill(px, w, 60, h - 60, 136, 8, new Color32(235, 235, 235, 255));
            Disc(px, w, 128, 230, 70, new Color32(250, 214, 180, 255));               // face
            Fill(px, w, 58, 280, 140, 22, new Color32(30, 30, 35, 255));               // hair
            Disc(px, w, 103, 240, 8, new Color32(30, 30, 35, 255));                     // eyes
            Disc(px, w, 153, 240, 8, new Color32(30, 30, 35, 255));
            Fill(px, w, 103, 195, 50, 6, new Color32(190, 70, 70, 255));                // smile
            Fill(px, w, 78, 100, 100, 60, new Color32(30, 50, 90, 255));                // collar
            var ink = new Color32(200, 20, 25, 255);
            switch (variant)
            {
                case 1: // moustache and a unibrow
                    Fill(px, w, 88, 210, 80, 10, ink); Fill(px, w, 80, 204, 14, 10, ink); Fill(px, w, 162, 204, 14, 10, ink);
                    Fill(px, w, 88, 256, 80, 6, ink);
                    break;
                case 2: // glasses and horns
                    Ring(px, w, 103, 240, 18, 4, ink); Ring(px, w, 153, 240, 18, 4, ink); Fill(px, w, 121, 240, 14, 4, ink);
                    Fill(px, w, 70, 300, 10, 40, ink); Fill(px, w, 176, 300, 10, 40, ink);
                    break;
                default: // crossed-out slogan and a speech bubble
                    for (int i = 0; i < 200; i++) { Fill(px, w, 28 + i, h - 76 + i * 60 / 200, 4, 4, ink); Fill(px, w, 28 + i, h - 16 - i * 60 / 200, 4, 4, ink); }
                    Ring(px, w, 205, 120, 36, 4, ink); Fill(px, w, 175, 150, 12, 12, ink);
                    break;
            }
            return SavePng(name, w, h, px);
        }

        // 0: clean painting (Fuji under the sun); 1-3: the same painting defaced with the marker.
        static Texture2D PaintingTexture(string name, int variant)
        {
            const int w = 384, h = 256;
            var px = new Color32[w * h];
            Fill(px, w, 0, 0, w, h, new Color32(150, 190, 225, 255));                  // sky
            Disc(px, w, 300, 200, 28, new Color32(240, 120, 60, 255));                   // sun
            for (int x = 0; x < w; x++)                                                  // Fuji with its snow cap
            {
                int top = Mathf.Max(0, 170 - Mathf.Abs(x - 160) * 7 / 8);
                Fill(px, w, x, 0, 1, top, new Color32(80, 95, 130, 255));
                if (top > 130) Fill(px, w, x, 130, 1, top - 130, new Color32(245, 245, 250, 255));
            }
            Fill(px, w, 0, 0, w, 40, new Color32(70, 120, 70, 255));                     // fields
            var ink = new Color32(200, 20, 25, 255);
            switch (variant)
            {
                case 1: // the sun gets a face and a moustache
                    Disc(px, w, 291, 208, 4, ink); Disc(px, w, 309, 208, 4, ink); Fill(px, w, 284, 190, 32, 5, ink);
                    break;
                case 2: // a UFO abducts the mountain top
                    Ring(px, w, 160, 225, 22, 4, ink); Fill(px, w, 130, 220, 60, 4, ink);
                    for (int i = 0; i < 40; i++) { Fill(px, w, 150 - i / 4, 185 - i, 2, 2, ink); Fill(px, w, 170 + i / 4, 185 - i, 2, 2, ink); }
                    break;
                default: // «Р+К» carved into the field and an arrow to it
                    Fill(px, w, 40, 10, 4, 22, ink); Ring(px, w, 48, 26, 6, 3, ink); Fill(px, w, 60, 19, 10, 3, ink); Fill(px, w, 64, 14, 3, 12, ink);
                    Fill(px, w, 78, 10, 4, 22, ink); for (int i = 0; i < 11; i++) { Fill(px, w, 82 + i, 21 + i, 3, 3, ink); Fill(px, w, 82 + i, 21 - i, 3, 3, ink); }
                    for (int i = 0; i < 60; i++) Fill(px, w, 110 + i, 40 + i / 2, 3, 3, ink);
                    break;
            }
            return SavePng(name, w, h, px);
        }

        static Texture2D CoverTexture(string name)
        {
            const int w = 512, h = 288;
            var px = new Color32[w * h];
            Fill(px, w, 0, 0, w, h, new Color32(120, 170, 210, 255));                 // sky
            for (int x = 0; x < w; x++)                                                 // the sleeping volcano
            {
                int top = 200 - Mathf.Abs(x - 256) * 3 / 4;
                if (x > 226 && x < 286) top = 178;
                Fill(px, w, x, 0, 1, Mathf.Max(0, top), new Color32(110, 95, 85, 255));
            }
            Fill(px, w, 140, 0, 232, 90, new Color32(235, 230, 220, 255));             // the school
            for (int i = 0; i < 6; i++) Fill(px, w, 155 + i * 36, 50, 24, 22, new Color32(70, 110, 150, 255));
            Fill(px, w, 236, 0, 40, 40, new Color32(90, 60, 40, 255));
            return SavePng(name, w, h, px);
        }

        static void Fill(Color32[] px, int w, int x0, int y0, int fw, int fh, Color32 c)
        {
            int h = px.Length / w;
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y0 + fh); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x0 + fw); x++)
                px[y * w + x] = c;
        }

        static void Disc(Color32[] px, int w, int cx, int cy, int r, Color32 c) => Ring(px, w, cx, cy, r, r, c);

        static void Ring(Color32[] px, int w, int cx, int cy, int r, int thickness, Color32 c)
        {
            int h = px.Length / w;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r && d >= r - thickness) px[y * w + x] = c;
            }
        }

        static Texture2D SavePng(string name, int w, int h, Color32[] px)
        {
            string path = $"{ArtDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- objects

        static GameObject Root(Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Init(Object target, string objectId, Object data)
        {
            var so = new SerializedObject(target);
            so.FindProperty("objectId").stringValue = objectId;
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildTap(Transform parent, Assets a, string id, string name, Vector3 pos)
        {
            var root = Root(parent, name, pos, 180f);
            // Local +Z points out of the wall into the room.
            Box(root.transform, "Base", new Vector3(0f, 1.05f, 0.03f), new Vector3(0.08f, 0.08f, 0.06f), a.metal, false);
            Box(root.transform, "Spout", new Vector3(0f, 1.07f, 0.11f), new Vector3(0.04f, 0.04f, 0.14f), a.metal, false);
            Box(root.transform, "Handle", new Vector3(0f, 1.13f, 0.05f), new Vector3(0.12f, 0.03f, 0.03f), a.metal, false);

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            water.transform.SetParent(root.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.93f, 0.17f);
            water.transform.localScale = new Vector3(0.025f, 0.12f, 0.025f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = a.water;
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            water.SetActive(false);

            // Generous trigger so the small tap is easy to aim at.
            var hit = root.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 1.05f, 0.12f);
            hit.size = new Vector3(0.4f, 0.35f, 0.3f);

            var tap = root.AddComponent<WaterTap>();
            Init(tap, id, a.tap);
            PlayerSliceSetup.Set(tap, "water", water.GetComponent<Renderer>());
        }

        static void BuildVending(Transform parent, Assets a, string id, string name, Vector3 pos, float yaw)
        {
            var root = Root(parent, name, pos, yaw);
            // Local +Z is the front.
            Box(root.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 0.8f), a.machine);
            var front = Box(root.transform, "Front", new Vector3(-0.1f, 1.15f, 0.405f), new Vector3(0.6f, 1.1f, 0.02f), a.machineScreen, false);
            Box(root.transform, "Slot", new Vector3(0.05f, 0.3f, 0.405f), new Vector3(0.6f, 0.2f, 0.02f), a.slot, false);
            var slot = new GameObject("CanSpawn").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(0.05f, 0.32f, 0.52f);

            var vm = root.AddComponent<VendingMachine>();
            Init(vm, id, a.vending);
            PlayerSliceSetup.Set(vm, "slot", slot);
            var so = new SerializedObject(vm);
            var lights = so.FindProperty("lights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = front.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildLocker(Transform parent, Assets a)
        {
            var root = Root(parent, "Locker_Ryuta", LockerPos, 180f);
            var t = root.transform;
            // Local +Z is the front; inside is x -0.24..0.24, z -0.24..0.25.
            Box(t, "Back", new Vector3(0f, 0.9f, -0.24f), new Vector3(0.5f, 1.8f, 0.02f), a.lockerBody);
            Box(t, "Left", new Vector3(-0.24f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.5f), a.lockerBody);
            Box(t, "Right", new Vector3(0.24f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.5f), a.lockerBody);
            Box(t, "Top", new Vector3(0f, 1.79f, 0f), new Vector3(0.5f, 0.02f, 0.5f), a.lockerBody);
            Box(t, "Bottom", new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.02f, 0.5f), a.lockerBody);
            Box(t, "ShelfTop", new Vector3(0f, 1.38f, 0f), new Vector3(0.46f, 0.02f, 0.46f), a.lockerBody);
            Box(t, "ShelfMiddle", new Vector3(0f, 0.72f, 0f), new Vector3(0.46f, 0.02f, 0.46f), a.lockerBody);

            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-0.25f, 0f, 0.255f);
            Box(hinge, "Door", new Vector3(0.25f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.02f), a.lockerDoor);
            Box(hinge, "Vent", new Vector3(0.25f, 1.55f, 0.012f), new Vector3(0.3f, 0.12f, 0.005f), a.slot, false);

            Detail(t, "Detail_Photo", 0, new Vector3(-0.08f, 1.08f, -0.225f), new Vector3(0.12f, 0.16f, 0.01f), a.photo);
            Detail(t, "Detail_Sneaker", 1, new Vector3(0.06f, 0.08f, 0f), new Vector3(0.11f, 0.12f, 0.3f), a.shoe);
            Detail(t, "Detail_Textbook", 2, new Vector3(0f, 1.42f, 0f), new Vector3(0.22f, 0.06f, 0.3f), a.book);
            Detail(t, "Detail_Bento", 3, new Vector3(0.05f, 0.77f, 0.02f), new Vector3(0.2f, 0.08f, 0.13f), a.bento);

            var camGo = new GameObject("CloseUpCamera");
            camGo.transform.SetParent(t, false);
            camGo.transform.localPosition = new Vector3(0.05f, 1.1f, 1.05f);
            camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.05f, -0.2f, -1.05f));
            var cam = camGo.AddComponent<CinemachineCamera>();
            cam.Lens.FieldOfView = 55f;
            cam.Lens.NearClipPlane = 0.05f;
            camGo.SetActive(false);

            var locker = root.AddComponent<RyutaLocker>();
            Init(locker, "locker_ryuta", a.locker);
            PlayerSliceSetup.Set(locker, "actions", a.input);
            PlayerSliceSetup.Set(locker, "door", hinge);
            PlayerSliceSetup.Set(locker, "closeUpCamera", cam);
        }

        static void Detail(Transform parent, string name, int index, Vector3 pos, Vector3 size, Material mat)
        {
            var go = Box(parent, name, pos, size, mat);
            go.AddComponent<InspectDetail>().index = index;
        }

        // A portrait poster (0.58 x 0.85) or a landscape painting (0.9 x 0.6) on the wall; local +Z faces the room.
        // The close-up camera in front of it is what the drawing mini-game looks through.
        static GameObject BuildPoster(Transform parent, Assets a, PosterData data, string id, string name, Vector3 pos, float yaw,
            bool landscape)
        {
            var root = Root(parent, name, pos, yaw);
            Vector2 size = landscape ? new Vector2(0.9f, 0.6f) : new Vector2(0.58f, 0.85f);
            float y = landscape ? 1.65f : 1.6f;
            Box(root.transform, "Frame", new Vector3(0f, y, 0.01f), new Vector3(size.x + 0.08f, size.y + 0.07f, 0.02f), landscape ? a.wood : a.frame);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Picture";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, y, 0.022f);
            // A quad faces -Z; turn it so the picture faces the corridor (+Z of the root).
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<Renderer>().sharedMaterial = landscape ? a.canvas : a.paper;

            var camGo = new GameObject("CloseUpCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, y, 1.25f);
            camGo.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var cam = camGo.AddComponent<CinemachineCamera>();
            cam.Lens.FieldOfView = 45f;
            cam.Lens.NearClipPlane = 0.05f;
            camGo.SetActive(false);

            var poster = root.AddComponent<DrawablePoster>();
            Init(poster, id, data);
            PlayerSliceSetup.Set(poster, "picture", quad.GetComponent<Renderer>());
            PlayerSliceSetup.Set(poster, "actions", a.input);
            PlayerSliceSetup.Set(poster, "closeUpCamera", cam);
            return root;
        }

        static void BuildShower(Transform parent, Assets a)
        {
            var root = Root(parent, "Shower_WC_M", ShowerPos, 0f);
            var t = root.transform;
            // Local +Z points out of the wall into the room.
            Box(t, "TileWall", new Vector3(0f, 1.1f, 0.01f), new Vector3(1f, 2.2f, 0.02f), a.tile, false);
            Box(t, "Tray", new Vector3(0f, 0.03f, 0.47f), new Vector3(0.9f, 0.06f, 0.9f), a.tray);
            Box(t, "Pipe", new Vector3(0f, 1.6f, 0.05f), new Vector3(0.04f, 1f, 0.04f), a.metal, false);
            Box(t, "Arm", new Vector3(0f, 2.1f, 0.18f), new Vector3(0.04f, 0.04f, 0.28f), a.metal, false);
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(t, false);
            head.transform.localPosition = new Vector3(0f, 2.06f, 0.32f);
            head.transform.localScale = new Vector3(0.18f, 0.02f, 0.18f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = a.metal;
            Box(t, "Valve", new Vector3(0f, 1.2f, 0.05f), new Vector3(0.14f, 0.04f, 0.06f), a.metal, false);

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            water.transform.SetParent(t, false);
            water.transform.localPosition = new Vector3(0f, 1.05f, 0.32f);
            water.transform.localScale = new Vector3(0.16f, 1f, 0.16f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = a.water;
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            water.SetActive(false);

            // Generous trigger over the valve and the stall, so it is easy to aim at.
            var hit = root.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 1.2f, 0.3f);
            hit.size = new Vector3(0.9f, 1.4f, 0.5f);

            var shower = root.AddComponent<ShowerHead>();
            Init(shower, "shower_wc_m", a.shower);
            PlayerSliceSetup.Set(shower, "water", water.GetComponent<Renderer>());
        }

        static void BuildPaint(Transform parent, Assets a)
        {
            if (a.paint == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pickup_Paint";
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(PaintPos, Quaternion.identity);
            go.transform.localScale = new Vector3(0.18f, 0.11f, 0.18f);
            go.GetComponent<Renderer>().sharedMaterial = a.paintCan;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(2.5f, 3f, 2.5f);
            var p = go.AddComponent<PickupItem>();
            PlayerSliceSetup.Set(p, "item", a.paint);
            var so = new SerializedObject(p);
            so.FindProperty("takenFlag").stringValue = "paint_taken";
            so.FindProperty("voiceHint").stringValue = "У кабинета рисования кто-то бросил банку краски…";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRack(Transform parent, Assets a)
        {
            var root = Root(parent, "BookletRack_Entrance", RackPos, 0f);
            var t = root.transform;
            // Local +Z faces into the corridor (north).
            Box(t, "Back", new Vector3(0f, 0.75f, -0.12f), new Vector3(0.8f, 1.5f, 0.03f), a.wood);
            Box(t, "SideL", new Vector3(-0.39f, 0.75f, 0f), new Vector3(0.03f, 1.5f, 0.26f), a.wood);
            Box(t, "SideR", new Vector3(0.39f, 0.75f, 0f), new Vector3(0.03f, 1.5f, 0.26f), a.wood);
            var colors = new[] { a.lockerDoor, a.machine, a.book };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.45f + i * 0.4f;
                Box(t, $"Shelf_{i}", new Vector3(0f, y, 0f), new Vector3(0.76f, 0.02f, 0.24f), a.wood);
                for (int j = 0; j < 3; j++)
                {
                    var booklet = Box(t, $"Booklet_{i}_{j}", new Vector3(-0.24f + j * 0.24f, y + 0.14f, -0.06f),
                        new Vector3(0.2f, 0.28f, 0.015f), colors[(i + j) % 3], false);
                    booklet.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                }
            }
            var rack = root.AddComponent<BookletRack>();
            Init(rack, "booklet_rack_entrance", a.booklet);
            PlayerSliceSetup.Set(rack, "actions", a.input);
        }

        static void BuildMarker(Transform parent, Assets a)
        {
            if (a.marker == null) { Debug.LogWarning("[InteractablesSetup] Item_Marker not found, the marker pickup is skipped."); return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pickup_Marker";
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(MarkerPos, Quaternion.Euler(0f, 35f, 90f));
            go.transform.localScale = new Vector3(0.035f, 0.075f, 0.035f);
            go.GetComponent<Renderer>().sharedMaterial = a.markerMat;
            // Easier to aim at than the thin cylinder.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 1.4f, 4f);
            var p = go.AddComponent<PickupItem>();
            PlayerSliceSetup.Set(p, "item", a.marker);
            var so = new SerializedObject(p);
            so.FindProperty("takenFlag").stringValue = "marker_taken";
            so.FindProperty("voiceHint").stringValue = "Во дворе, у скамейки, что-то валяется…";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Bootstrap services

        [MenuItem("Tools/Funseki/Save/Add inventory and save services to Bootstrap")]
        public static void AddServicesToBootstrap()
        {
            Directory.CreateDirectory(SaveDir);
            var saveSettings = Asset<SaveSettings>($"{SaveDir}/SaveSettings.asset", _ => { });
            var invSettings = AssetDatabase.LoadAssetAtPath<InventorySettings>(InventorySettingsPath);

            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);
            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null)
            {
                Debug.LogError("[InteractablesSetup] No Bootstrap object in the Bootstrap scene.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            bool changed = false;
            if (boot.GetComponentInChildren<InventoryService>(true) == null && invSettings != null)
            {
                var inv = new GameObject("InventoryService");
                inv.transform.SetParent(boot.transform, false);
                PlayerSliceSetup.Set(inv.AddComponent<InventoryService>(), "settings", invSettings);
                PlayerSliceSetup.Set(inv.AddComponent<InventoryPanel>(), "settings", invSettings);
                changed = true;
            }
            if (boot.GetComponentInChildren<SaveService>(true) == null)
            {
                var save = new GameObject("SaveService");
                save.transform.SetParent(boot.transform, false);
                PlayerSliceSetup.Set(save.AddComponent<SaveService>(), "settings", saveSettings);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[InteractablesSetup] [Bootstrap]: InventoryService + InventoryPanel and SaveService are in place.");
            }
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
