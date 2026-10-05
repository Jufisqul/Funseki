using UnityEngine;

namespace Funseki.Player
{
    // Feeds PlayerMotor into the hero's Animator: Speed (m/s) drives Idle/Walk/Run,
    // Grounded switches to the airborne pose, Jump fires on take-off.
    public class PlayerAnimator : MonoBehaviour
    {
        public const string SpeedParam = "Speed";
        public const string GroundedParam = "Grounded";
        public const string JumpParam = "Jump";

        [SerializeField] PlayerSettings settings;
        [SerializeField] PlayerMotor motor;
        [SerializeField] Animator animator;

        static readonly int Speed = Animator.StringToHash(SpeedParam);
        static readonly int Grounded = Animator.StringToHash(GroundedParam);
        static readonly int Jump = Animator.StringToHash(JumpParam);

        float airTime;

        void OnEnable() => motor.Jumped += OnJumped;
        void OnDisable() => motor.Jumped -= OnJumped;

        void OnJumped()
        {
            airTime = settings.airborneDelay;
            animator.SetTrigger(Jump);
        }

        void Update()
        {
            // CharacterController.isGrounded flickers on steps; short airtime still counts as grounded.
            airTime = motor.IsGrounded ? 0f : airTime + Time.deltaTime;
            animator.SetFloat(Speed, motor.PlanarSpeed, settings.animatorSpeedDamping, Time.deltaTime);
            animator.SetBool(Grounded, airTime < settings.airborneDelay);
        }
    }
}
