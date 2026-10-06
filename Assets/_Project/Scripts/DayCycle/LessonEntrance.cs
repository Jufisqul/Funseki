using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.DayCycle
{
    // Trigger at a classroom door: the lesson with this lessonId starts when the hero walks in after the bell.
    // Needs a trigger collider; the kinematic Rigidbody makes trigger events fire for the CharacterController.
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public class LessonEntrance : MonoBehaviour
    {
        [SerializeField] string lessonId = "fizra";
        [SerializeField] string playerTag = "Player";

        static readonly List<LessonEntrance> all = new();

        public string LessonId => lessonId;
        public bool PlayerInside { get; private set; }

        public static LessonEntrance Find(string id)
        {
            foreach (var e in all)
                if (e.lessonId == id) return e;
            return null;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void OnEnable() => all.Add(this);

        void OnDisable()
        {
            all.Remove(this);
            PlayerInside = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            PlayerInside = true;
            GameEvents.RaiseLessonEntranceReached(lessonId);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag)) PlayerInside = false;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
