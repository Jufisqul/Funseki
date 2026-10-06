using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Pranks
{
    // «Укрытие» (GDD 5.4): a box (trigger BoxCollider on this object) where the «Шум» falls faster — the toilet,
    // behind the shoe lockers. NoiseMeter asks HideZone.Contains for the controlled hero.
    [RequireComponent(typeof(BoxCollider))]
    public class HideZone : MonoBehaviour
    {
        [Tooltip("For the debug log: «Туалет М»")]
        [SerializeField] string displayName;

        static readonly List<HideZone> Active = new();
        BoxCollider box;

        public string DisplayName => displayName;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        bool ContainsPoint(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>The zone the point is in, or null.</summary>
        public static HideZone Find(Vector3 world)
        {
            foreach (var z in Active)
                if (z != null && z.ContainsPoint(world)) return z;
            return null;
        }

        void OnDrawGizmos()
        {
            var b = box != null ? box : GetComponent<BoxCollider>();
            if (b == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
