using Funseki.Core;
using UnityEngine;

namespace Funseki.Inventory
{
    // On the hero. Shows the selected item's handPrefab in the right hand (humanoid bone),
    // placed by ItemData.handPosition / handRotation / handScale.
    // The inventory is shared by the three heroes, so only the hero under control holds the item.
    public class HeldItemView : MonoBehaviour
    {
        [SerializeField] Animator animator;

        Transform hand;
        GameObject shown;

        void OnEnable()
        {
            GameEvents.OnSelectedItemChanged += Show;
            GameEvents.OnHeroSwitched += OnHeroSwitched;
            Show(SelectedItem());
        }

        void OnDisable()
        {
            GameEvents.OnSelectedItemChanged -= Show;
            GameEvents.OnHeroSwitched -= OnHeroSwitched;
            Show(null);
        }

        void OnHeroSwitched(GameObject previous, GameObject current, float time) => Show(SelectedItem());

        static ItemData SelectedItem() => ServiceLocator.TryGet<Inventory>(out var inv) ? inv.Selected : null;

        void Show(ItemData item)
        {
            if (shown != null) Destroy(shown);
            shown = null;
            if (item == null || item.handPrefab == null || !HeroService.IsLeader(gameObject)) return;

            if (hand == null && animator != null && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) hand = transform;

            shown = Instantiate(item.handPrefab, hand);
            shown.name = "Held_" + item.id;
            shown.transform.SetLocalPositionAndRotation(item.handPosition, Quaternion.Euler(item.handRotation));
            shown.transform.localScale = Vector3.one * item.handScale;
            // A held item must not push the hero or become an interaction target.
            foreach (var col in shown.GetComponentsInChildren<Collider>()) Destroy(col);
        }
    }
}
