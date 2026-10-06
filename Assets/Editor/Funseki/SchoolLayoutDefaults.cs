using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Fills SchoolLayout from the step 1 table (Assets/_Project/School/Docs/Step1_PlanAnalysis.md).
    // The asset is the source of truth afterwards; this only runs on demand.
    public static class SchoolLayoutDefaults
    {
        public const string LayoutPath = KitGenerator.Root + "Data/SchoolLayout.asset";

        [MenuItem("Tools/Funseki/Reset School Layout To Plan")]
        public static void ResetToPlan()
        {
            var layout = LoadOrCreate();
            if (layout.zones.Count > 0 && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Reset School Layout",
                    "Replace every zone in SchoolLayout with the zones from the floor plans?", "Replace", "Cancel"))
                return;
            Fill(layout);
        }

        public static SchoolLayout LoadOrCreate()
        {
            var layout = AssetDatabase.LoadAssetAtPath<SchoolLayout>(LayoutPath);
            if (layout != null) return layout;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LayoutPath));
            layout = ScriptableObject.CreateInstance<SchoolLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            Fill(layout);
            return layout;
        }

        public static void Fill(SchoolLayout layout)
        {
            var z = new List<ZoneData>();

            // ---------- Floor 1: north row ----------
            z.Add(Room("utility_1f", "Хозкомната", 1, 0, 0, 6, 8, Furnish.Storage, false, D(Side.South, 2, OpeningType.DoorSwing)));
            z.Add(Room("art", "Рисование", 1, 6, 0, 8, 8, Furnish.Classroom, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Room("heroes_class", "Контрольная (класс героев)", 1, 14, 0, 8, 8, Furnish.Classroom, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Room("geometry", "Геометрия (кабинет №4)", 1, 22, 0, 8, 8, Furnish.GeometryClass, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Room("music", "Музыка", 1, 30, 0, 8, 8, Furnish.Classroom, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Room("physics", "Физика", 1, 38, 0, 8, 8, Furnish.Classroom, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Stair("stairs_ne", "Лестница СВ", 1, 46, 0, 6, 8, Side.South));

            // ---------- Floor 1: corridor ring, courtyard, south row ----------
            z.Add(Corridor("corridor_n_1f", "Коридор, север", 1, 0, 8, 52, 4, "corr1"));
            z.Add(Corridor("corridor_w_1f", "Коридор, запад", 1, 0, 12, 6, 25, "corr1"));
            z.Add(Corridor("corridor_e_1f", "Коридор, восток", 1, 46, 12, 6, 25, "corr1"));
            var corridorS = Corridor("corridor_s_1f", "Коридор со шкафчиками для обуви", 1, 0, 37, 52, 5, "corr1",
                D(Side.South, 24, OpeningType.DoorDouble));
            corridorS.furnish = Furnish.EntranceCorridor;
            z.Add(corridorS);

            z.Add(new ZoneData
            {
                zoneId = "courtyard", displayName = "Внутренний двор", floor = 1, group = ZoneGroup.Floor_1,
                kind = ZoneKind.Outdoor, rect = new RectInt(6, 12, 40, 18), furnish = Furnish.Courtyard,
                openings = new List<Opening>
                {
                    D(Side.North, 18, OpeningType.DoorDouble), D(Side.South, 18, OpeningType.DoorDouble),
                    D(Side.West, 7, OpeningType.DoorDouble), D(Side.East, 7, OpeningType.DoorDouble),
                },
            });

            z.Add(Room("shop", "Труд (мастерская)", 1, 6, 30, 8, 7, Furnish.Workshop, true, D(Side.South, 5, OpeningType.DoorSliding)));
            z.Add(Room("shop_2", "Труд (склад мастерской)", 1, 14, 30, 9, 7, Furnish.Workshop, true, D(Side.South, 6, OpeningType.DoorSliding)));
            z.Add(Corridor("passage_s", "Проход во двор", 1, 23, 30, 6, 7, "corr1"));
            z.Add(Room("anatomy", "Анатомия", 1, 29, 30, 9, 7, Furnish.Anatomy, true, D(Side.South, 1, OpeningType.DoorSliding)));
            z.Add(Room("literature", "Литература", 1, 38, 30, 8, 7, Furnish.Classroom, true, D(Side.South, 1, OpeningType.DoorSliding)));

            z.Add(Stair("stairs_sw", "Лестница ЮЗ", 1, 0, 42, 10, 6, Side.North));
            z.Add(Room("elevator", "Лифт (с 1-го на 1-й)", 1, 42, 42, 5, 6, Furnish.Elevator, false, D(Side.East, 2, OpeningType.Doorway)));
            z.Add(Corridor("elevator_hall", "Холл у лифта", 1, 47, 42, 5, 6, "corr1"));

            // ---------- Floor 1: west wing, gym (opens on day 2) ----------
            z.Add(Gym(Room("weights", "Качалка", 1, -22, 10, 10, 6, Furnish.Weights, true, D(Side.South, 5, OpeningType.DoorDouble))));
            z.Add(Gym(Room("equipment", "Инвентарь", 1, -12, 10, 4, 6, Furnish.Storage, false, D(Side.South, 1, OpeningType.DoorSwing))));
            z.Add(Gym(Room("pe_office", "Кабинет физрука", 1, -8, 10, 8, 6, Furnish.Office, true,
                D(Side.South, 2, OpeningType.DoorSwing), D(Side.East, 2, OpeningType.DoorSwing))));
            var gym = Gym(Room("gym", "Спортзал (Физра)", 1, -22, 16, 22, 16, Furnish.Gym, true,
                D(Side.East, 6, OpeningType.DoorDouble), D(Side.South, 18, OpeningType.DoorDouble)));
            gym.heightFloors = 2;
            z.Add(gym);
            z.Add(Gym(Room("locker_a", "Раздевалка Ж", 1, -10, 32, 6, 5, Furnish.LockerRoom, false, D(Side.East, 1, OpeningType.DoorSwing))));
            z.Add(Gym(Room("locker_b", "Раздевалка М", 1, -10, 37, 6, 5, Furnish.LockerRoom, false, D(Side.East, 1, OpeningType.DoorSwing))));
            var vestibule = Gym(Corridor("gym_vestibule", "Тамбур спортзала", 1, -4, 32, 4, 10, "",
                D(Side.East, 6, OpeningType.DoorDouble)));
            z.Add(vestibule);

            // ---------- Floor 1: east wing ----------
            var canteen = Room("canteen", "Столовая", 1, 52, 10, 22, 12, Furnish.Canteen, true,
                D(Side.West, 0, OpeningType.Open, 4), D(Side.South, 6, OpeningType.Open, 4));
            canteen.unlockDay = 3;
            z.Add(canteen);
            var kitchen = Room("kitchen", "Кухня", 1, 62, 22, 12, 10, Furnish.Kitchen, true,
                D(Side.North, 5, OpeningType.Doorway), D(Side.South, 4, OpeningType.DoorSwing));
            kitchen.unlockDay = 3;
            z.Add(kitchen);
            z.Add(new ZoneData
            {
                zoneId = "kitchen_yard", displayName = "Технический выход с мусорками", floor = 1,
                group = ZoneGroup.Floor_1, kind = ZoneKind.Outdoor, rect = new RectInt(62, 32, 11, 4),
                furnish = Furnish.KitchenYard,
            });
            z.Add(Room("wc_f_1f", "Туалет Ж", 1, 52, 22, 6, 8, Furnish.Toilets, false, D(Side.West, 3, OpeningType.DoorSwing)));
            z.Add(Room("wc_m_1f", "Туалет М", 1, 52, 30, 6, 8, Furnish.Toilets, false, D(Side.West, 3, OpeningType.DoorSwing)));
            z.Add(Corridor("passage_e", "Проход у столовой", 1, 58, 22, 4, 16, "corr1"));
            var hall = Corridor("hall_e", "Восточный холл", 1, 52, 38, 10, 4, "corr1");
            hall.furnish = Furnish.Hall;
            z.Add(hall);

            // ---------- Floor 2 ----------
            var stairs3 = Stair("stairs_3f", "Лестница на 3-й этаж", 2, 0, 0, 6, 8, Side.South);
            stairs3.heightFloors = 2;
            stairs3.topLanding = true;
            z.Add(stairs3);
            z.Add(new ZoneData
            {
                zoneId = "floor_3", displayName = "Третий этаж (дверь «Совещание идёт»)", floor = 3,
                group = ZoneGroup.Floor_2, kind = ZoneKind.Void, rect = new RectInt(0, 8, 6, 4), unlockDay = 7,
                openings = new List<Opening> { D(Side.North, 4, OpeningType.DoorSwing) },
            });

            for (int i = 0; i < 5; i++)
            {
                var dorm = Room("dorm_" + (i + 1), "Жилая комната " + (i + 1), 2, 6 + i * 8, 0, 8, 8, Furnish.Dorm, true,
                    D(Side.South, 3, OpeningType.DoorSliding));
                dorm.group = ZoneGroup.Dorm;
                z.Add(dorm);
            }
            z.Add(StairTop("stairs_ne_top", "Лестница СВ, 2-й этаж", 2, 46, 0, 6, 8, Side.South));

            z.Add(Corridor("corridor_n_2f", "Коридор 2-го этажа, север", 2, 0, 8, 52, 4, "corr2"));
            z.Add(Corridor("corridor_w_2f", "Коридор 2-го этажа, запад", 2, 0, 12, 6, 25, "corr2"));
            z.Add(Corridor("corridor_e_2f", "Коридор 2-го этажа, восток", 2, 46, 12, 6, 25, "corr2"));
            z.Add(Corridor("corridor_s_2f", "Коридор 2-го этажа, юг", 2, 0, 37, 52, 5, "corr2"));

            var staff = Room("staff_room", "Учительская", 2, 6, 30, 16, 7, Furnish.StaffRoom, true,
                D(Side.West, 2, OpeningType.DoorSwing), D(Side.South, 10, OpeningType.DoorSliding));
            staff.unlockDay = 4;
            z.Add(staff);
            z.Add(Room("wc_a_2f", "Туалет Ж, 2-й этаж", 2, 22, 30, 4, 7, Furnish.Toilets, false, D(Side.South, 2, OpeningType.DoorSwing)));
            z.Add(Room("wc_b_2f", "Туалет М, 2-й этаж", 2, 26, 30, 4, 7, Furnish.Toilets, false, D(Side.South, 0, OpeningType.DoorSwing)));
            z.Add(Room("detention", "Комната наказаний", 2, 30, 30, 8, 7, Furnish.Detention, true, D(Side.South, 4, OpeningType.DoorSliding)));
            z.Add(Room("utility_2f", "Хозкомната, 2-й этаж", 2, 38, 30, 8, 7, Furnish.Storage, false, D(Side.East, 1, OpeningType.DoorSwing)));
            z.Add(StairTop("stairs_sw_top", "Лестница ЮЗ, 2-й этаж", 2, 0, 42, 10, 6, Side.North));

            var laundry = Room("laundry", "Прачечная", 2, -10, 32, 10, 10, Furnish.Laundry, true, D(Side.East, 5, OpeningType.DoorDouble));
            laundry.group = ZoneGroup.Dorm;
            z.Add(laundry);

            z.Add(Room("assembly_foyer", "Фойе актового зала", 2, 52, 10, 10, 8, Furnish.None, true,
                D(Side.West, 0, OpeningType.Open, 4), D(Side.East, 2, OpeningType.DoorDouble)));
            var showersM = Room("showers_m", "Душевые М", 2, 52, 18, 6, 14, Furnish.Showers, false, D(Side.West, 2, OpeningType.DoorSwing));
            showersM.group = ZoneGroup.Dorm;
            z.Add(showersM);
            var showersF = Room("showers_f", "Душевые Ж", 2, 58, 18, 4, 14, Furnish.Showers, false, D(Side.North, 1, OpeningType.DoorSwing));
            showersF.group = ZoneGroup.Dorm;
            z.Add(showersF);
            z.Add(Room("assembly_hall", "Актовый зал", 2, 62, 10, 12, 22, Furnish.Assembly, true));
            var principal = Room("principal", "Кабинет директора", 2, 52, 32, 10, 10, Furnish.Office, true, D(Side.West, 6, OpeningType.DoorSwing));
            principal.unlockDay = 5;
            z.Add(principal);

            layout.zones = z;
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Funseki] SchoolLayout filled from the plan: {z.Count} zones.");
        }

        static Opening D(Side side, int offset, OpeningType type, int width = 0) => new Opening(side, offset, type, width);

        static ZoneData Room(string id, string name, int floor, int x, int y, int w, int d, Furnish furnish,
            bool windows, params Opening[] openings) => new ZoneData
        {
            zoneId = id, displayName = name, floor = floor, kind = ZoneKind.Room,
            group = floor == 1 ? ZoneGroup.Floor_1 : ZoneGroup.Floor_2,
            rect = new RectInt(x, y, w, d), furnish = furnish, autoWindows = windows,
            openings = new List<Opening>(openings),
        };

        static ZoneData Corridor(string id, string name, int floor, int x, int y, int w, int d, string group,
            params Opening[] openings)
        {
            var zone = Room(id, name, floor, x, y, w, d, Furnish.None, true, openings);
            zone.kind = ZoneKind.Corridor;
            zone.openGroup = group;
            return zone;
        }

        static ZoneData Stair(string id, string name, int floor, int x, int y, int w, int d, Side entry)
        {
            var zone = Room(id, name, floor, x, y, w, d, Furnish.None, false,
                D(entry, 0, OpeningType.Open, entry == Side.North || entry == Side.South ? w : d));
            zone.kind = ZoneKind.Stair;
            zone.stairEntry = entry;
            return zone;
        }

        static ZoneData StairTop(string id, string name, int floor, int x, int y, int w, int d, Side entry)
        {
            var zone = Stair(id, name, floor, x, y, w, d, entry);
            zone.kind = ZoneKind.StairTop;
            return zone;
        }

        static ZoneData Gym(ZoneData zone)
        {
            zone.group = ZoneGroup.Gym;
            zone.unlockDay = 2;
            return zone;
        }
    }
}
