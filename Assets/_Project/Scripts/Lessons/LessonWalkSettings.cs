using UnityEngine;

namespace Funseki.Lessons
{
    // How the teacher and the students walk to a lesson after the bell (LessonWalk).
    // Assets/_Project/Data/Lessons/LessonWalk_Fizra.asset.
    [CreateAssetMenu(fileName = "LessonWalk", menuName = "Funseki/Lessons/Lesson Walk")]
    public class LessonWalkSettings : ScriptableObject
    {
        [Tooltip("The walk starts when this break phase ends (its bell)")]
        public string afterPhaseId = "break_1";
        [Tooltip("Seconds after the bell before they set off")]
        [Min(0f)] public float startDelay = 1.5f;
        [Tooltip("Walking speed, m/s")]
        [Min(0.1f)] public float speed = 1.7f;
        [Tooltip("Turning speed, degrees/s")]
        public float turnSpeed = 360f;
        [Tooltip("Each next walker starts this many seconds after the previous one")]
        [Min(0f)] public float followDelay = 0.8f;
        [Tooltip("Where they stop: the walkers stand side by side at the last point of the route, this far apart")]
        [Min(0.3f)] public float standSpacing = 0.9f;

        [Header("Doors on the way")]
        [Tooltip("A school door within this distance of a door point of the route is opened (and unlocked) at the bell")]
        [Min(0.5f)] public float doorRadius = 2f;

        [Header("Lines")]
        public string[] teacherStartLines = { "Фьють! *машет: за мной*", "Фьють-фьють! *показывает на спортзал*" };
        public string[] studentLines = { "Опять физра…", "Он сегодня злой?", "Я забыл форму. Опять." };
        [Tooltip("Chance that a student says a line when the walk starts")]
        [Range(0f, 1f)] public float studentLineChance = 0.5f;
    }
}
