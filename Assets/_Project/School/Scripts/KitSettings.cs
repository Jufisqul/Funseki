using UnityEngine;

namespace Funseki.School
{
    // Every size of the greybox kit and the school builder lives here, in meters.
    // Modules are generated from these numbers, so change them and regenerate the kit.
    [CreateAssetMenu(menuName = "Funseki/School/Kit Settings", fileName = "KitSettings")]
    public class KitSettings : ScriptableObject
    {
        [Header("Grid")]
        public float gridStep = 1f;
        public float wallModuleWidth = 2f;

        [Header("Floors")]
        public float floorHeight = 3.5f;
        public float ceilingHeight = 3.2f;
        public float slabThickness = 0.3f;
        public float ceilingPanelThickness = 0.05f;

        [Header("Walls")]
        public float wallThickness = 0.2f;
        public float cornerSize = 0.2f;
        public float columnSize = 0.4f;

        [Header("Rooms and corridors")]
        public float corridorWidth = 3f;          // minimum, from the brief
        public float corridorWidthNorthSouth = 4f; // as drawn on the plan
        public float corridorWidthWestEast = 6f;   // as drawn on the plan
        public float classroomSize = 8f;

        [Header("Openings")]
        public float doorWidth = 1.2f;
        public float doorHeight = 2.2f;
        public float doubleDoorWidth = 2.4f;
        public float doubleDoorHeight = 2.4f;
        public float doubleDoorModuleWidth = 4f;
        public float doorThickness = 0.05f;
        public float windowWidth = 1.6f;
        public float windowHeight = 1.2f;
        public float windowSillHeight = 0.9f;

        [Header("Stairs and railings")]
        public float stairWidth = 2f;
        public int stairStepsPerFlight = 10;      // one flight climbs half a floor
        public float stairTread = 0.3f;
        public float railingHeight = 1f;
        public float stageHeight = 1f;

        [Header("Colors")]
        public Color wallColor = new Color(0.82f, 0.82f, 0.82f);
        public Color floorColor = new Color(0.35f, 0.35f, 0.37f);
        public Color doorColor = new Color(0.22f, 0.42f, 0.85f);
        public Color propColor = new Color(0.95f, 0.55f, 0.15f);
        public Color interactiveColor = new Color(1f, 0.85f, 0.1f);

        [Header("Grid texture")]
        public int gridTextureSize = 256;
        public int gridLinePixels = 4;
        [Range(0f, 1f)] public float gridLineDarkness = 0.35f;

        public float StairRise => floorHeight * 0.5f / stairStepsPerFlight;
        public float StairRun => stairTread * stairStepsPerFlight;
    }
}
