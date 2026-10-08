using UnityEngine;

namespace Funseki.Inventory
{
    // The shelf in room 7 for gacha figures (CLAUDE.md «Полный День 1», steps 8–9): one spot per figure.
    // Empty for now; the figures come with the gacha machine and the Diary collections (task 21).
    public class CollectionShelf : MonoBehaviour
    {
        [Tooltip("Spots for the figures, left to right")]
        [SerializeField] Transform[] slots;

        public int Capacity => slots != null ? slots.Length : 0;
        public int Count { get; private set; }

        /// <summary>Puts a model on the next free spot; false when the shelf is full.</summary>
        public bool Place(GameObject prefab)
        {
            if (prefab == null || Count >= Capacity) return false;
            var model = Instantiate(prefab, slots[Count++]);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return true;
        }

        void OnDrawGizmos()
        {
            if (slots == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            foreach (var s in slots)
                if (s != null) Gizmos.DrawWireCube(s.position + s.up * 0.08f, new Vector3(0.12f, 0.16f, 0.12f));
        }
    }
}
