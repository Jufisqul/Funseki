using UnityEngine;

namespace Funseki.Core
{
    // The shared color palette of the school (visual board «Визуальное направление», GDD 9.1).
    // Tools > Funseki > School Style bakes these colors into the school textures and materials,
    // so a designer changes a color here and presses Apply again: nothing in the scene moves.
    [CreateAssetMenu(menuName = "Funseki/Visual/School Palette", fileName = "SchoolPalette")]
    public class SchoolPalette : ScriptableObject
    {
        [Header("Board palette (8 colors)")]
        [Tooltip("Вулканический пепел: контур, тени")] public Color ash = Hex("#2B2F3A");
        [Tooltip("Школьный мел: панели, светлые поверхности")] public Color chalk = Hex("#F4F1E8");
        [Tooltip("Магма: акцент")] public Color magma = Hex("#FF6B3D");
        [Tooltip("Закат в окне: солнце, предметы, ручки")] public Color sunset = Hex("#F2B134");
        [Tooltip("Линолеум: стены, коридор")] public Color linoleum = Hex("#4FA3A5");
        [Tooltip("Дымка: небо, стекло, блики")] public Color haze = Hex("#A9C9D8");
        [Tooltip("Жвачка: гэги, женские кабинки")] public Color gum = Hex("#FF9FB2");
        [Tooltip("Вечерняя тень: вулкан, закат")] public Color dusk = Hex("#5A3D6B");

        [Header("Walls")]
        public Color plaster = Hex("#E9E2CF");
        public Color wainscot = Hex("#4FA3A5");
        public Color trim = Hex("#4A4F5E");
        public Color baseboard = Hex("#2B2F3A");
        public Color wallTile = Hex("#E4EEF0");
        public Color wallTileGrout = Hex("#A9C9D8");
        public Color gymPanel = Hex("#C9A574");
        public Color facadePlinth = Hex("#8A7F6A");
        [Tooltip("Height of the colored band at the bottom of corridor and classroom walls, m")]
        public float wainscotHeight = 1.0f;
        [Tooltip("Height of the tiled part of toilet, shower and kitchen walls, m")]
        public float wallTileHeight = 1.6f;
        [Tooltip("Height of the wooden panels in the gym, m")]
        public float gymPanelHeight = 1.8f;

        [Header("Floors")]
        public Color corridorFloor = Hex("#6FA39B");
        public Color corridorFloorAlt = Hex("#5E918A");
        public Color classroomWood = Hex("#A98458");
        public Color gymWood = Hex("#D2AE78");
        public Color wetTile = Hex("#B8CCD3");
        public Color wetTileGrout = Hex("#8FB2C2");
        public Color carpet = Hex("#B5483A");
        public Color carpetPattern = Hex("#D86A4F");
        public Color ground = Hex("#8A7F6A");
        public Color groundSpeck = Hex("#B9B19C");
        public Color concrete = Hex("#B9B19C");

        [Header("Ceiling")]
        public Color ceiling = Hex("#EEEADF");
        public Color ceilingLine = Hex("#CFC8B6");

        [Header("Doors and windows")]
        public Color slidingDoorFrame = Hex("#8C6A45");
        public Color slidingDoorPanel = Hex("#D8C9A8");
        public Color swingDoor = Hex("#5FA09A");
        public Color swingDoorPanel = Hex("#7FB5AD");
        public Color doorGlass = Hex("#A9C9D8");
        public Color handle = Hex("#F2B134");
        public Color windowFrame = Hex("#C9D1D6");
        public Color windowGlass = new Color(0.66f, 0.79f, 0.85f, 0.28f);

        [Header("Furniture and props")]
        public Color deskWood = Hex("#C9A574");
        public Color metal = Hex("#8B9BA0");
        public Color darkMetal = Hex("#4A4F5E");
        public Color lockerSteel = Hex("#9DB3B5");
        public Color blackboard = Hex("#3E5E4A");
        public Color stallMale = Hex("#A9C9D8");
        public Color stallFemale = Hex("#FF9FB2");
        public Color porcelain = Hex("#F4F1E8");
        public Color fabric = Hex("#A9C9D8");
        public Color vendingBody = Hex("#FF6B3D");
        public Color stone = Hex("#9AA5AD");
        public Color trashBin = Hex("#4FA3A5");

        [Header("Texture look")]
        [Tooltip("Texture density. 64 px per meter reads as early-2000s low-res")]
        [Range(16, 256)] public int pixelsPerMeter = 64;
        [Tooltip("How much the surfaces are mottled (0 = flat fill)")]
        [Range(0f, 0.3f)] public float noise = 0.07f;
        [Tooltip("Crunchy PS2-era texels instead of smooth filtering")]
        public bool pointFilter = false;
        [Range(0f, 1f)] public float smoothness = 0.12f;

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
