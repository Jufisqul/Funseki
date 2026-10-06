using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.DayCycle
{
    // The structure of one school day (GDD 3.2) and the bell rules (GDD 3.4). Edited by the designer.
    // Phases run top to bottom; after the last one the game switches to endState.
    [CreateAssetMenu(fileName = "DaySchedule", menuName = "Funseki/DayCycle/Day Schedule")]
    public class DaySchedule : ScriptableObject
    {
        [Range(1, 7)] public int day = 1;
        public List<DayPhase> phases = new();

        [Header("After the last phase")]
        [Tooltip("SliceEnd for the demo")]
        public GameState endState = GameState.SliceEnd;

        [Header("Bell (GDD 3.4)")]
        [Tooltip("Default break length when a Break phase has maxDuration 0, seconds")]
        [Min(1f)] public float defaultBreakDuration = 480f;
        [Tooltip("Seconds after the bell before the hero counts as late (WorldFlag lesson_late, the teacher's gag)")]
        [Min(0f)] public float lateAfterBell = 90f;
        [Tooltip("Seconds after the bell before the second bell and the hint arrow to the classroom")]
        [Min(0f)] public float secondBellAfter = 60f;
        [Tooltip("WorldFlag set when the hero is late")]
        public string lateFlag = "lesson_late";

        public float MaxDuration(DayPhase phase) =>
            phase.type == DayPhaseType.Break && phase.end.maxDuration <= 0f ? defaultBreakDuration : phase.end.maxDuration;
    }
}
