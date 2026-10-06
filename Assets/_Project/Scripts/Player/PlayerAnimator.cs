using Funseki.Core;
using UnityEngine;

namespace Funseki.Player
{
    // Feeds PlayerMotor into the hero's Animator: Speed (m/s) drives Idle/Walk/Run,
    // Grounded switches to the airborne pose, Jump fires on take-off, Talking holds the talking loop in a dialogue.
    // A follower hero (controller off, moved by a NavMeshAgent) gets Speed from how fast it actually moves.
    public class PlayerAnimator : MonoBehaviour
    {
        public const string SpeedParam = "Speed";
        public const string GroundedParam = "Grounded";
        public const string JumpParam = "Jump";
        public const string TalkingParam = "Talking";
        public const string HitParam = "Hit";

        [SerializeField] PlayerSettings settings;
        [SerializeField] PlayerMotor motor;
        [SerializeField] Animator animator;

        static readonly int Speed = Animator.StringToHash(SpeedParam);
        static readonly int Grounded = Animator.StringToHash(GroundedParam);
        static readonly int Jump = Animator.StringToHash(JumpParam);
        static readonly int Talking = Animator.StringToHash(TalkingParam);

        CharacterController cc;
        float airTime;
        Vector3 lastPosition;
        bool hasTalking;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            lastPosition = transform.position;
            foreach (var p in animator.parameters)
                if (p.nameHash == Talking) hasTalking = true;
        }

        void OnEnable()
        {
            motor.Jumped += OnJumped;
            GameEvents.OnDialogueStarted += OnDialogueStarted;
            GameEvents.OnDialogueEnded += OnDialogueEnded;
        }

        void OnDisable()
        {
            motor.Jumped -= OnJumped;
            GameEvents.OnDialogueStarted -= OnDialogueStarted;
            GameEvents.OnDialogueEnded -= OnDialogueEnded;
        }

        void OnJumped()
        {
            airTime = settings.airborneDelay;
            animator.SetTrigger(Jump);
        }

        void OnDialogueStarted(GameObject npc, GameObject hero)
        {
            if (hero == gameObject && hasTalking) animator.SetBool(Talking, true);
        }

        void OnDialogueEnded(GameObject npc, GameObject hero)
        {
            if (hero == gameObject && hasTalking) animator.SetBool(Talking, false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            bool controlled = cc == null || cc.enabled;
            float speed = motor.PlanarSpeed;
            if (!controlled)
            {
                delta.y = 0f;
                speed = dt > 0f ? delta.magnitude / dt : 0f;
                // Sub-centimetre shifts of a standing NavMeshAgent must not wake up the walk cycle.
                if (speed < 0.2f) speed = 0f;
            }

            // CharacterController.isGrounded flickers on steps; short airtime still counts as grounded.
            airTime = !controlled || motor.IsGrounded ? 0f : airTime + dt;
            animator.SetFloat(Speed, speed, settings.animatorSpeedDamping, dt);
            animator.SetBool(Grounded, airTime < settings.airborneDelay);
        }
    }
}
