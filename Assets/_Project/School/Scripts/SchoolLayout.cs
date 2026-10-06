using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.School
{
    // Plan coordinates, in whole meters: x grows east (right on the plan), y grows south
    // (down on the plan). Origin is the north-west corner of the main block.
    // In the scene: world X = x, world Z = -y, world Y = (floor - 1) * floorHeight.

    public enum ZoneKind
    {
        Room,       // walls, floor, ceiling where nothing is above
        Corridor,   // corridors sharing an openGroup have no walls between them
        Outdoor,    // floor, no ceiling, no walls to the outside
        Stair,      // two flights from this floor up to the next one
        StairTop,   // the stairwell on the upper floor: only the arrival strip has a floor
        Void,       // no floor; used for the locked third floor behind the meeting door
    }

    public enum ZoneGroup { Floor_1, Floor_2, Dorm, Gym }

    public enum Side { North, South, West, East }

    public enum OpeningType { DoorSwing, DoorSliding, DoorDouble, Doorway, Open, Window }

    public enum Furnish
    {
        None, Classroom, GeometryClass, Workshop, Anatomy, Dorm, Toilets, Showers, Laundry,
        Canteen, Kitchen, KitchenYard, Gym, LockerRoom, Weights, Storage, Office, StaffRoom,
        Detention, Assembly, Elevator, Courtyard, EntranceCorridor, Hall,
    }

    [Serializable]
    public class Opening
    {
        public Side side;
        [Tooltip("Meters from the start of the side: west end for North/South, north end for West/East.")]
        public int offset;
        public OpeningType type;
        [Tooltip("Only for Open: width of the gap in meters. Doors and windows use the kit module width.")]
        public int width;
        [Tooltip("0 = the zone's own floor, 1 = one floor up (tall zones).")]
        public int floorOffset;

        public Opening() { }

        public Opening(Side side, int offset, OpeningType type, int width = 0, int floorOffset = 0)
        {
            this.side = side;
            this.offset = offset;
            this.type = type;
            this.width = width;
            this.floorOffset = floorOffset;
        }
    }

    [Serializable]
    public class ZoneData
    {
        public string zoneId;
        public string displayName;
        [Min(1)] public int floor = 1;
        public ZoneGroup group;
        public ZoneKind kind;
        [Tooltip("x, y, width, depth in plan meters")]
        public RectInt rect;
        [Min(1)] public int heightFloors = 1;
        [Range(1, 7)] public int unlockDay = 1;
        public bool autoWindows;
        [Tooltip("Zones with the same non-empty group have no walls between them.")]
        public string openGroup;
        [Tooltip("Stair and StairTop: side the flights start and arrive on (North or South).")]
        public Side stairEntry = Side.South;
        [Tooltip("Stair only: also add an arrival strip one floor up (stairs to the third floor).")]
        public bool topLanding;
        public Furnish furnish;
        public List<Opening> openings = new List<Opening>();

        public int TopFloor => floor + heightFloors - 1;
    }

    [CreateAssetMenu(menuName = "Funseki/School/School Layout", fileName = "SchoolLayout")]
    public class SchoolLayout : ScriptableObject
    {
        public List<ZoneData> zones = new List<ZoneData>();
    }
}
