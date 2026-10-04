using UnityEngine;

// Feeds PlayerController state into the hero's Animator (Hero.controller):
// Speed in m/s drives the Idle/Walk/Run blend, Grounded switches to the Airborne jump pose.
public class PlayerAnimator : MonoBehaviour
{
    public const string SpeedParam = "Speed";
    public const string GroundedParam = "Grounded";
    public const string VerticalSpeedParam = "VerticalSpeed";

    public PlayerController player;
    public Animator animator;
    public float speedDamping = 0.1f;
    // CharacterController.isGrounded flickers on steps and slopes; ignore airtime shorter than this.
    public float airborneDelay = 0.12f;

    static readonly int Speed = Animator.StringToHash(SpeedParam);
    static readonly int Grounded = Animator.StringToHash(GroundedParam);
    static readonly int VerticalSpeed = Animator.StringToHash(VerticalSpeedParam);

    float airTime;

    void Update()
    {
        Vector3 v = player.Velocity;
        airTime = player.IsGrounded ? 0f : airTime + Time.deltaTime;
        bool jumpedUp = v.y > 1f;

        animator.SetFloat(Speed, new Vector2(v.x, v.z).magnitude, speedDamping, Time.deltaTime);
        animator.SetBool(Grounded, airTime < airborneDelay && !jumpedUp);
        animator.SetFloat(VerticalSpeed, v.y);
    }
}
