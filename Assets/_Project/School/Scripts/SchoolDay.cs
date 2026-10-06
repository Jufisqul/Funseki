using UnityEngine;

namespace Funseki.School
{
    // The current day of the week (1..7). Locked doors compare against it.
    // Until the day cycle exists it starts at 1 every play session.
    public static class SchoolDay
    {
        public static int Current { get; set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Current = 1;
    }
}
