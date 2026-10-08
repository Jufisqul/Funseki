using UnityEngine;

namespace Funseki.NPC
{
    // On a humanoid NPC model: when a gameplay script turns the "source" transform away from its rest pose
    // (the PE teacher's Arm in the Fizra lesson points at the class or the door), the model's upper arm follows
    // its forward. Runs after the Animator, so the animated pose is kept everywhere else.
    [RequireComponent(typeof(Animator))]
    public class NpcArmPointer : MonoBehaviour
    {
        [SerializeField] Transform source;
        [SerializeField] HumanBodyBones upperArm = HumanBodyBones.RightUpperArm;
        [SerializeField] HumanBodyBones lowerArm = HumanBodyBones.RightLowerArm;
        [SerializeField] HumanBodyBones hand = HumanBodyBones.RightHand;
        [SerializeField] NpcSettings settings;

        Transform upper, lower, end;
        Quaternion rest;
        float weight;

        void Start()
        {
            var animator = GetComponent<Animator>();
            if (animator.isHuman)
            {
                upper = animator.GetBoneTransform(upperArm);
                lower = animator.GetBoneTransform(lowerArm);
                end = animator.GetBoneTransform(hand);
            }
            if (source != null) rest = source.localRotation;
        }

        public void SetSource(Transform arm) => source = arm;

        void LateUpdate()
        {
            if (source == null || upper == null || lower == null || end == null) return;
            bool pointing = Quaternion.Angle(source.localRotation, rest) > 2f;
            float speed = settings != null ? settings.armPointSpeed : 10f;
            weight = Mathf.MoveTowards(weight, pointing ? 1f : 0f, speed * Time.deltaTime);
            if (weight <= 0f) return;

            // Straighten the elbow, then swing the whole arm onto the source's forward.
            lower.rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(end.position - lower.position, lower.position - upper.position), weight) * lower.rotation;
            Vector3 dir = end.position - upper.position;
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(dir, source.forward), weight) * upper.rotation;
        }
    }
}
