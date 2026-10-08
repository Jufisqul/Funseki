using UnityEngine;

namespace Funseki.Inventory
{
    // The wall in room 7 for prank trophies (CLAUDE.md «Полный День 1», steps 8–9; the whistle is the first):
    // 14 spots, one per trophy of the week. Empty for now; filled with the Diary collections (task 21).
    public class TrophyWall : MonoBehaviour
    {
        [Tooltip("Spots for the trophies, row by row")]
        [SerializeField] Transform[] slots;

        public int Capacity => slots != null ? slots.Length : 0;
        public int Count { get; private set; }

        /// <summary>Hangs a model on the next free spot; false when the wall is full.</summary>
        public bool Hang(GameObject prefab)
        {
            if (prefab == null || Count >= Capacity) return false;
            var model = Instantiate(prefab, slots[Count++]);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return true;
        }

        void OnDrawGizmos()
        {
            if (slots == null) return;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            foreach (var s in slots)
                if (s != null) Gizmos.DrawWireCube(s.position, new Vector3(0.3f, 0.3f, 0.05f));
        }
    }
}
