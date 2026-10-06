using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

namespace Funseki.Lessons
{
    // Where a lesson happens in the day scene (the PE area in the courtyard, a classroom…). The mini-game prefab is
    // spawned as its child. Places the three heroes on their spots and hides the break-time props for the lesson.
    public class LessonStage : MonoBehaviour
    {
        [SerializeField] string lessonId = "fizra";
        [Tooltip("Hero spots: 0 = the party leader (centre), 1 and 2 = the other two. Forward = where they look")]
        [SerializeField] Transform[] heroSpots = new Transform[0];
        [Tooltip("Scene objects switched off for the lesson and back on after it (the break-time teacher, students…)")]
        [SerializeField] GameObject[] hideDuringLesson = new GameObject[0];
        [Tooltip("Plays LessonOutcomeScene.timeline; created on demand when empty")]
        [SerializeField] PlayableDirector outcomeDirector;

        static readonly List<LessonStage> all = new();
        readonly List<GameObject> hidden = new();

        public string LessonId => lessonId;

        public static LessonStage Find(string id)
        {
            foreach (var s in all)
                if (s.lessonId == id) return s;
            return null;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public void Begin(GameObject leader, GameObject[] others)
        {
            hidden.Clear();
            foreach (var go in hideDuringLesson)
                if (go != null && go.activeSelf) { go.SetActive(false); hidden.Add(go); }

            if (leader != null && heroSpots.Length > 0) Place(leader, heroSpots[0]);
            int spot = 1;
            foreach (var hero in others)
            {
                if (hero == null || hero == leader) continue;
                if (spot < heroSpots.Length) Place(hero, heroSpots[spot]);
                spot++;
            }
        }

        public void End()
        {
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
        }

        public void PlayTimeline(PlayableAsset asset)
        {
            if (asset == null) return;
            if (outcomeDirector == null)
            {
                outcomeDirector = gameObject.AddComponent<PlayableDirector>();
                outcomeDirector.playOnAwake = false;
            }
            outcomeDirector.playableAsset = asset;
            outcomeDirector.Play();
        }

        // A hero on a CharacterController (the leader) or on a NavMeshAgent (a follower) — both have to be moved their own way.
        static void Place(GameObject hero, Transform spot)
        {
            if (spot == null) return;
            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.Warp(spot.position);
                hero.transform.rotation = spot.rotation;
                return;
            }
            var cc = hero.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            hero.transform.SetPositionAndRotation(spot.position, spot.rotation);
            if (cc != null) cc.enabled = wasEnabled;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            foreach (var s in heroSpots)
            {
                if (s == null) continue;
                Gizmos.DrawWireSphere(s.position + Vector3.up * 0.1f, 0.35f);
                Gizmos.DrawLine(s.position + Vector3.up * 0.1f, s.position + Vector3.up * 0.1f + s.forward * 0.7f);
            }
        }
    }
}
