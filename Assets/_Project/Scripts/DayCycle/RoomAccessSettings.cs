using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.DayCycle
{
    // Which school rooms (zones) are open in the morning, the day and the evening.
    // The time of day comes from the current DayPhase.timeOfDay: Dawn = morning, Sun = day, Sunset = evening.
    // A closed room locks every door that touches it; a zone's own unlockDay (LockedDoor) still applies on top.
    // Read by RoomAccessDirector. The room list is filled from School_Greybox by
    // Tools > Funseki > DayCycle > Setup room access (fill rooms from the school).
    [CreateAssetMenu(fileName = "RoomAccess", menuName = "Funseki/DayCycle/Room Access")]
    public class RoomAccessSettings : ScriptableObject
    {
        [Serializable]
        public class Room
        {
            [Tooltip("Label for the Inspector: floor, room name and zone id (filled by the setup menu)")]
            public string name;
            [Tooltip("Zone id in School_Greybox (Zone_<id>)")]
            public string zoneId;
            [Tooltip("Open in the morning (phases with Time Of Day = Dawn)")]
            public bool morning = true;
            [Tooltip("Open in the day (phases with Time Of Day = Sun)")]
            public bool day = true;
            [Tooltip("Open in the evening (phases with Time Of Day = Sunset)")]
            public bool evening = true;

            public bool IsOpen(TimeOfDayIcon time) => time switch
            {
                TimeOfDayIcon.Dawn => morning,
                TimeOfDayIcon.Sunset => evening,
                _ => day,
            };
        }

        [Tooltip("One entry per school zone. A zone missing from the list is always open")]
        public List<Room> rooms = new();

        [Header("Texts")]
        [Tooltip("What the hero says at a door closed for this time of day")]
        public string closedLine = "Сейчас тут закрыто.";

        [Header("Doors")]
        [Tooltip("A door that touches a zone (its trigger box grown by this many metres) belongs to that zone")]
        [Min(0f)] public float doorTouchDistance = 0.75f;
        [Tooltip("When a room closes, its open doors swing shut, unless a hero stands closer than this (m)")]
        [Min(0f)] public float keepOpenNearHero = 3f;

        public bool IsOpen(string zoneId, TimeOfDayIcon time)
        {
            foreach (var r in rooms)
                if (r.zoneId == zoneId) return r.IsOpen(time);
            return true;
        }
    }
}
