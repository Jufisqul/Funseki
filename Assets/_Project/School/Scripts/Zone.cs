using UnityEngine;

namespace Funseki.School
{
    // Marks a school zone. Lives next to a trigger BoxCollider covering the zone.
    public class Zone : MonoBehaviour
    {
        public string zoneId;
        public string displayName;
        public int floor = 1;
        [Range(1, 7)] public int unlockDay = 1;
    }
}
