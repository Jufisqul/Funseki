using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using static PlayerTestSceneBuilder;

// Editor-only: grey box of Day 1 (GDD 5.8, 11): the schoolyard and the first floor.
// Layout follows the menu: gate south, school north, toilet block and bike shelter west.
// 1F, west to east: craft room, room No. 4 (rug instead of a board), the heroes' class, entrance hall
// with shoe lockers, the stairwell to the "Meeting in progress" door, the staff room (locked until Day 4)
// and the one-floor lift. Yellow boxes are interactables, coloured capsules are NPC spots.
// Re-runnable: it rebuilds the scene from scratch.
public static class Day1GreyboxBuilder
{
    const string ScenePath = "Assets/_Game/Scenes/Day1_Greybox.unity";
    const string ControlsPath = "Assets/_Game/Input/GameControls.inputactions";
    const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";

    const float H = 3.5f;       // storey height
    const float T = 0.2f;       // wall thickness
    const float DoorW = 1.4f;
    const float DoorH = 2.3f;
    const float CorridorZ = 9f; // rooms z 0..9, corridor z 9..12
    const float BackZ = 12f;

    const string Wall = "#DEDAD3", Floor = "#B9B2A6", Ceiling = "#ECE9E3", Desk = "#C9A26B", Dark = "#3B4A63";
    const string Interactive = "#FFD23F", Locked = "#C8553D";

    static TMP_FontAsset font;

    [MenuItem("Tools/One Funseki/Build Day 1 Greybox")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);

        BuildLighting();
        var root = new GameObject("Day1").transform;
        BuildYard(Group(root, "Yard"));
        BuildSchool(Group(root, "School_1F"));

        // Morning arrival: the hero walks in through the gate.
        var spawn = new GameObject("Spawn_Morning").transform;
        spawn.SetParent(root, false);
        spawn.position = new Vector3(0, 0.05f, -28f);

        var player = BuildPlayer(controls, out var target, out var body);
        player.transform.SetPositionAndRotation(spawn.position, Quaternion.identity);
        BuildCameras(controls, player, target, body);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[One Funseki] Day 1 grey box built: " + ScenePath);
    }

    // ================================================================= yard

    static void BuildYard(Transform yard)
    {
        Box(yard, "Ground", new Vector3(0, -0.5f, -8), new Vector3(66, 1, 50), "Yard", "#D2C0A2");
        Box(yard, "Path_FromGate", new Vector3(0, 0.01f, -18.5f), new Vector3(3, 0.02f, 25), "Paving", "#CFCAC0");
        Box(yard, "Path_EntrancePlaza", new Vector3(3, 0.011f, -3), new Vector3(10, 0.02f, 6), "Paving", "#CFCAC0");

        // Fence with the gate in the south wall.
        var fence = Group(yard, "Fence");
        WallX(fence, "Fence_S", -31f, -32f, 32f, 0, 2f, "Concrete", (0f, 5f));
        WallZ(fence, "Fence_W", -32f, -31f, 15f, 0, 2f, "Concrete");
        WallZ(fence, "Fence_E", 32f, -31f, 15f, 0, 2f, "Concrete");
        WallX(fence, "Fence_N", 15f, -32f, 32f, 0, 2f, "Concrete");
        Box(fence, "GatePillar_L", new Vector3(-2.8f, 1.4f, -31), new Vector3(0.6f, 2.8f, 0.6f), "Concrete", "#BDB8AE");
        Box(fence, "GatePillar_R", new Vector3(2.8f, 1.4f, -31), new Vector3(0.6f, 2.8f, 0.6f), "Concrete", "#BDB8AE");
        Label(fence, "Ворота", new Vector3(0, 3.2f, -31.4f), 0, 1.2f);

        // PE ground (Fizra, Day 1 lesson 1): track, inner field, cones, the bench where Grunge sits.
        var pe = Group(yard, "PE_Ground");
        Box(pe, "Track", new Vector3(19, 0.01f, -18), new Vector3(20, 0.02f, 18), "Track", "#C8553D");
        Box(pe, "Field", new Vector3(19, 0.02f, -18), new Vector3(14, 0.02f, 12), "Lawn", "#A3B884");
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cylinder, pe, "Cone" + i, new Vector3(14 + i * 2.5f, 0.2f, -18), new Vector3(0.3f, 0.2f, 0.3f), "Cone", "#FF8A2B");
        Box(pe, "Bench", new Vector3(8.5f, 0.25f, -18), new Vector3(0.5f, 0.5f, 3), "Wood", "#8C6239");
        Label(pe, "Физра: площадка", new Vector3(19, 2.6f, -27.5f), 0, 1.4f);
        Npc(pe, "Физрук", new Vector3(19, 0, -15), "#2F5D9E");
        Npc(pe, "Ученик", new Vector3(16, 0, -20), "#7A8288");
        Npc(pe, "Ученица", new Vector3(22, 0, -21), "#7A8288");

        // West: outdoor toilet block (yeast prank later) and the bike shelter, as in the menu.
        var west = Group(yard, "West");
        Room(west, "ToiletBlock", -27, -21, -20, -14, 2.8f, "Wall", Wall, (z: -14f, x: -23.5f));
        Label(west, "Туалет", new Vector3(-23.5f, 3.2f, -13.85f), 180, 1f);
        var shelter = Group(west, "BikeShelter");
        Box(shelter, "Roof", new Vector3(-24, 2.3f, -6.5f), new Vector3(10, 0.15f, 3.5f), "Metal", "#7A8288");
        for (int i = 0; i < 4; i++)
        {
            Box(shelter, "Post", new Vector3(-28.7f + i * 3.1f, 1.15f, -8.1f), new Vector3(0.12f, 2.3f, 0.12f), "Metal", "#7A8288");
            Box(shelter, "Bike", new Vector3(-28 + i * 2.6f, 0.5f, -6.5f), new Vector3(0.15f, 1f, 1.7f), "BikeFrame", "#C0392B");
        }
        Label(west, "Велопарковка", new Vector3(-24, 3f, -8.4f), 0, 1f);

        // Cherry trees along the fence: the "living school" backdrop.
        foreach (var x in new[] { -26f, -18f, -10f, 10f, 26f })
        {
            Prim(PrimitiveType.Cylinder, yard, "Trunk", new Vector3(x, 1.2f, -28.5f), new Vector3(0.35f, 1.2f, 0.35f), "Bark", "#5A3B32");
            Prim(PrimitiveType.Sphere, yard, "Crown", new Vector3(x, 3.2f, -28.5f), new Vector3(3.2f, 2.4f, 3.2f), "Sakura", "#F6A9C6");
        }

        Npc(yard, "Ученик", new Vector3(-6, 0, -12), "#7A8288");
        Npc(yard, "Ученица", new Vector3(-4.8f, 0, -12.6f), "#7A8288");
        Label(yard, "Школа «Фунсэки»", new Vector3(4, 4.4f, -0.15f), 0, 2f);
    }

    // ================================================================= school, first floor

    static void BuildSchool(Transform school)
    {
        Box(school, "Floor", new Vector3(0, -0.04f, 6), new Vector3(48, 0.12f, 12), "Floor", Floor);

        // Shell. The stairwell (x 8..14) is double height for the stairs to the door to nowhere.
        var shell = Group(school, "Shell");
        WallX(shell, "Facade_S_West", 0, -24, 8, 0, H, "Wall", (4f, 3f));
        WallX(shell, "Facade_S_Stair", 0, 8, 14, 0, 2 * H, "Wall");
        WallX(shell, "Facade_S_East", 0, 14, 24, 0, H, "Wall");
        WallX(shell, "Back_N", BackZ, -24, 24, 0, H, "Wall");
        WallZ(shell, "End_W", -24, 0, BackZ, 0, H, "Wall");
        WallZ(shell, "End_E", 24, 0, BackZ, 0, H, "Wall");
        Box(shell, "Ceiling_West", new Vector3(-8, H + 0.05f, 6), new Vector3(32, 0.1f, 12), "Ceiling", Ceiling);
        Box(shell, "Ceiling_StairCorridor", new Vector3(11, H + 0.05f, 10.5f), new Vector3(6, 0.1f, 3), "Ceiling", Ceiling);
        Box(shell, "Ceiling_East", new Vector3(19, H + 0.05f, 6), new Vector3(10, 0.1f, 12), "Ceiling", Ceiling);
        Box(shell, "Ceiling_Stairwell", new Vector3(11, 2 * H + 0.05f, 4.5f), new Vector3(6, 0.1f, 9), "Ceiling", Ceiling);

        // Corridor wall with a door into every room; the entrance hall and stairwell open wide.
        WallX(shell, "CorridorWall", CorridorZ, -24, 24, 0, H, "Wall",
            (-17f, DoorW), (-9f, DoorW), (-7f, DoorW), (-1f, DoorW), (4f, 7.6f), (11f, 5.6f), (15f, DoorW), (22f, 1.2f));
        foreach (var x in new[] { -16f, -8f, 0f, 20f })
            WallZ(shell, "Partition", x, 0, CorridorZ, 0, H, "Wall");
        WallZ(shell, "Partition_StairW", 8, 0, CorridorZ, 0, 2 * H, "Wall");
        WallZ(shell, "Partition_StairE", 14, 0, CorridorZ, 0, 2 * H, "Wall");
        Box(shell, "Stairwell_UpperBack", new Vector3(11, H + H / 2, CorridorZ), new Vector3(6, H, T), "Wall", Wall);

        Corridor(Group(school, "Corridor"));
        CraftRoom(Group(school, "Room_Craft"));
        Room4(Group(school, "Room_04"));
        HeroClass(Group(school, "Room_Class1B"));
        Entrance(Group(school, "EntranceHall"));
        Stairwell(Group(school, "Stairwell"));
        StaffRoom(Group(school, "StaffRoom"));
        Lift(Group(school, "Lift"));
    }

    static void Corridor(Transform p)
    {
        for (float x = -20; x <= 20; x += 8) Lamp(p, new Vector3(x, H - 0.3f, 10.5f), 7);
        Npc(p, "Королева", new Vector3(-4, 0, 10.6f), "#E58DB0");
        Npc(p, "Литератор", new Vector3(-12, 0, 10.6f), "#5E9E47");
        // The school bell: target of the "Bell" prank (Day 2+). Here it just rings for lessons.
        Interactable(p, "Звонок", new Vector3(6.5f, 2.6f, 11.85f), new Vector3(0.4f, 0.4f, 0.2f));
    }

    static void CraftRoom(Transform p)
    {
        Door(p, "Кабинет труда", -17);
        Lamp(p, new Vector3(-20, H - 0.3f, 4.5f), 8);
        for (int i = 0; i < 3; i++)
            Box(p, "Workbench", new Vector3(-21.5f + i * 2.5f, 0.45f, 4), new Vector3(1.2f, 0.9f, 3), "Wood", "#8C6239");
        Box(p, "ToolRack", new Vector3(-23.8f, 1.2f, 4.5f), new Vector3(0.2f, 1.6f, 4), "Metal", "#7A8288");
        Interactable(p, "Отвёртка", new Vector3(-19, 0.95f, 3), new Vector3(0.3f, 0.08f, 0.08f));
    }

    // GDD 1.4: a rug hangs instead of the board, teachers write on it with chalk.
    static void Room4(Transform p)
    {
        Door(p, "Кабинет №4", -9);
        Lamp(p, new Vector3(-12, H - 0.3f, 4.5f), 9);
        Box(p, "Rug_InsteadOfBoard", new Vector3(-15.85f, 1.6f, 4.5f), new Vector3(0.06f, 1.4f, 3.6f), "Rug", "#9C3D3D");
        Box(p, "TeacherDesk", new Vector3(-14.4f, 0.4f, 4.5f), new Vector3(0.8f, 0.8f, 1.6f), "Desk", Desk);
        Desks(p, -13f, -9f);
        // Prank "Hide the chalk" (spec in Notion): the box on the teacher's desk.
        Interactable(p, "Мел (ChalkBox)", new Vector3(-14.4f, 0.87f, 5f), new Vector3(0.25f, 0.1f, 0.15f));
    }

    static void HeroClass(Transform p)
    {
        Door(p, "Класс 1-В", -1);
        Door(p, null, -7);
        Lamp(p, new Vector3(-4, H - 0.3f, 4.5f), 9);
        Box(p, "Blackboard", new Vector3(-7.85f, 1.6f, 4.5f), new Vector3(0.06f, 1.2f, 3.6f), "Blackboard", "#2E4A3A");
        Box(p, "TeacherDesk", new Vector3(-6.4f, 0.4f, 4.5f), new Vector3(0.8f, 0.8f, 1.6f), "Desk", Desk);
        Desks(p, -5f, -1f);
        Interactable(p, "Записка", new Vector3(-3f, 0.78f, 3f), new Vector3(0.15f, 0.02f, 0.1f));
        Npc(p, "Шутник", new Vector3(-2.2f, 0, 6.3f), "#FF8A2B");
    }

    // Japanese school entrance: shoe lockers (getabako) between the yard door and the corridor.
    static void Entrance(Transform p)
    {
        Lamp(p, new Vector3(4, H - 0.3f, 4.5f), 9);
        foreach (var x in new[] { 1.4f, 6.6f })
            Box(p, "ShoeLockers", new Vector3(x, 0.9f, 5), new Vector3(0.5f, 1.8f, 4), "Lockers", "#8E9AA0");
        Box(p, "DoorMat", new Vector3(4, 0.03f, 1.2f), new Vector3(3, 0.04f, 1.6f), "Mat", "#3B4A63");
        Label(p, "Вход", new Vector3(4, 2.9f, 0.13f), 180, 1f);
    }

    // GDD 1.4: stairs that lead to a closed door marked "Meeting in progress". Day 7 opens it via the lift.
    static void Stairwell(Transform p)
    {
        const int steps = 16;
        const float run = 0.35f, rise = H / steps;
        for (int i = 0; i < steps; i++)
        {
            float top = (i + 1) * rise;
            Box(p, "Step" + i, new Vector3(10, top / 2, 8.6f - (i + 0.5f) * run), new Vector3(2, top, run), "Stairs", "#BDB8AE");
        }
        float landingZ0 = 8.6f - steps * run;
        Box(p, "Landing", new Vector3(11, H / 2, (landingZ0 + T) / 2), new Vector3(5.8f, H, landingZ0 - T), "Stairs", "#BDB8AE");
        Box(p, "Door_MeetingInProgress", new Vector3(13.85f, H + DoorH / 2, 1.6f), new Vector3(0.12f, DoorH, 1.2f), "Locked", Locked);
        Label(p, "Совещание идёт", new Vector3(13.75f, H + DoorH + 0.35f, 1.6f), 90, 0.9f);
        Label(p, "Лестница", new Vector3(11, 2.9f, CorridorZ + 0.13f), 180, 1f);
        Lamp(p, new Vector3(11, 2 * H - 0.4f, 4), 10);
    }

    static void StaffRoom(Transform p)
    {
        LockedDoor(p, "Учительская (с Дня 4)", 15);
        Lamp(p, new Vector3(17, H - 0.3f, 4.5f), 8);
        for (int i = 0; i < 4; i++)
            Box(p, "StaffDesk", new Vector3(16 + (i % 2) * 2.2f, 0.4f, 3 + (i / 2) * 2.5f), new Vector3(1.6f, 0.8f, 1.2f), "Desk", Desk);
        Npc(p, "Учителя", new Vector3(18.5f, 0, 7), "#5E9E47");
    }

    // GDD 1.4: the only lift is one floor high -- from the first to the first.
    static void Lift(Transform p)
    {
        Door(p, "Лифт: 1 → 1", 22, 1.2f);
        Room(p, "LiftCab", 21, 23, 6.6f, CorridorZ - T / 2, 2.6f, "LiftCab", "#A7ADB3", (z: CorridorZ - T / 2, x: 22f));
        Interactable(p, "Кнопка «1»", new Vector3(22.9f, 1.2f, 7.8f), new Vector3(0.05f, 0.15f, 0.15f));
        Box(p, "Storage", new Vector3(22, 0.9f, 2.5f), new Vector3(3, 1.8f, 1), "Lockers", "#8E9AA0");
    }

    // ================================================================= pieces

    static void Desks(Transform p, float xFront, float xBack)
    {
        for (float x = xFront; x <= xBack + 0.01f; x += 1.3f)
            for (float z = 1.5f; z <= 7.6f; z += 1.5f)
            {
                Box(p, "Desk", new Vector3(x, 0.36f, z), new Vector3(0.45f, 0.72f, 0.6f), "Desk", Desk);
                Box(p, "Chair", new Vector3(x + 0.45f, 0.22f, z), new Vector3(0.38f, 0.44f, 0.38f), "Chair", "#5B3E28");
            }
    }

    static void Door(Transform p, string label, float x, float width = DoorW)
    {
        Box(p, "DoorFrame", new Vector3(x, DoorH + 0.03f, CorridorZ + 0.12f), new Vector3(width + 0.1f, 0.06f, 0.04f), "Frame", "#8E9AA0");
        if (label != null) Label(p, label, new Vector3(x, DoorH + 0.45f, CorridorZ + 0.13f), 180, 0.8f);
    }

    static void LockedDoor(Transform p, string label, float x)
    {
        Box(p, "LockedDoor", new Vector3(x, DoorH / 2, CorridorZ), new Vector3(DoorW, DoorH, 0.1f), "Locked", Locked);
        Label(p, label, new Vector3(x, DoorH + 0.45f, CorridorZ + 0.13f), 180, 0.8f);
    }

    // A closed box room: four walls, one door gap on the wall at doorZ, centred at door x.
    static void Room(Transform p, string name, float x0, float x1, float z0, float z1, float h, string mat, string hex, (float z, float x) door)
    {
        var r = Group(p, name);
        WallX(r, name + "_S", z0, x0, x1, 0, h, mat, Mathf.Approximately(door.z, z0) ? new[] { (door.x, DoorW) } : new (float, float)[0]);
        WallX(r, name + "_N", z1, x0, x1, 0, h, mat, Mathf.Approximately(door.z, z1) ? new[] { (door.x, DoorW) } : new (float, float)[0]);
        WallZ(r, name + "_W", x0, z0, z1, 0, h, mat);
        WallZ(r, name + "_E", x1, z0, z1, 0, h, mat);
        Box(r, name + "_Roof", new Vector3((x0 + x1) / 2, h + 0.05f, (z0 + z1) / 2), new Vector3(x1 - x0 + T, 0.1f, z1 - z0 + T), mat, hex);
    }

    // Wall along X at z from x0 to x1, with door gaps (centre, width); a lintel closes each gap above the door.
    static void WallX(Transform p, string name, float z, float x0, float x1, float y0, float h, string mat, params (float c, float w)[] doors)
    {
        float x = x0;
        foreach (var (c, w) in doors.OrderBy(d => d.c))
        {
            Seg(p, name, new Vector3((x + c - w / 2) / 2, y0 + h / 2, z), new Vector3(c - w / 2 - x, h, T), mat);
            if (h > DoorH + 0.05f)
                Seg(p, name + "_Lintel", new Vector3(c, y0 + (DoorH + h) / 2, z), new Vector3(w, h - DoorH, T), mat);
            x = c + w / 2;
        }
        Seg(p, name, new Vector3((x + x1) / 2, y0 + h / 2, z), new Vector3(x1 - x, h, T), mat);
    }

    static void WallZ(Transform p, string name, float x, float z0, float z1, float y0, float h, string mat)
        => Seg(p, name, new Vector3(x, y0 + h / 2, (z0 + z1) / 2), new Vector3(T, h, z1 - z0), mat);

    static void Seg(Transform p, string name, Vector3 pos, Vector3 size, string mat)
    {
        if (size.x < 0.01f || size.z < 0.01f) return;
        Box(p, name, pos, size, mat, mat == "Concrete" ? "#BDB8AE" : mat == "LiftCab" ? "#A7ADB3" : Wall);
    }

    static void Lamp(Transform p, Vector3 pos, float range)
    {
        var l = new GameObject("Lamp").AddComponent<Light>();
        l.transform.SetParent(p, false);
        l.transform.position = pos;
        l.type = LightType.Point;
        l.range = range;
        l.intensity = 2.2f;
        l.color = PlayerTestSceneBuilder.Hex("#FFF4D6");
        l.shadows = LightShadows.None;
    }

    static void Interactable(Transform p, string name, Vector3 pos, Vector3 size)
    {
        var go = Box(p, "Interactable_" + name, pos, size, "Interactive", Interactive);
        Label(go.transform.parent, name, pos + Vector3.up * 0.35f, 180, 0.35f, billboardFriendly: true);
    }

    static void Npc(Transform p, string name, Vector3 feet, string hex)
    {
        Prim(PrimitiveType.Capsule, p, "NPC_" + name, feet + Vector3.up * 0.875f, new Vector3(0.55f, 0.875f, 0.55f), "NPC_" + hex.TrimStart('#'), hex);
        Label(p, name, feet + Vector3.up * 2.05f, 180, 0.45f, billboardFriendly: true);
    }

    // World-space sign. rotY 180 reads from +Z looking toward -Z (e.g. from the corridor).
    static void Label(Transform p, string text, Vector3 pos, float rotY, float size, bool billboardFriendly = false)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(p, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size * 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = PlayerTestSceneBuilder.Hex("#1C1B22");
        tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, text.Length * size * 0.6f), size * 1.5f);
        if (billboardFriendly) go.AddComponent<FaceCamera>();
    }

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }
}
