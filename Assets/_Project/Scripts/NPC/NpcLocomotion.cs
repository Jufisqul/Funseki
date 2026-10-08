using UnityEngine;

namespace Funseki.NPC
{
    // On an NPC model (child of the NPC's "Body"): feeds the animator's Speed from how fast the NPC actually moves,
    // whatever moves it (WhistleTeacher, LessonWalk, a Timeline). Controller: Art/Animation/NPC.controller.
    [RequireComponent(typeof(Animator))]
    public class NpcLocomotion : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] NpcSettings settings;
        [Tooltip("Random start offset of the idle loop, so a crowd doesn't breathe in sync")]
        [SerializeField] bool randomizeStart = true;

        Animator animator;
        Vector3 lastPos;
        float speed;

        void Awake() => animator = GetComponent<Animator>();

        void OnEnable()
        {
            lastPos = transform.position;
            speed = 0f;
            if (randomizeStart && animator.runtimeAnimatorController != null)
                animator.Play(0, 0, Random.value);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            float measured = new Vector2(delta.x, delta.z).magnitude / dt;
            float damping = settings != null ? settings.speedDamping : 0.15f;
            speed = Mathf.Lerp(speed, measured, damping > 0f ? 1f - Mathf.Exp(-dt / damping) : 1f);
            float idle = settings != null ? settings.idleBelowSpeed : 0.1f;
            animator.SetFloat(SpeedId, speed < idle ? 0f : speed);
        }
    }
}
