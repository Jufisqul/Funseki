using Funseki.Core;
using UnityEngine;

namespace Funseki.Story
{
    // Room 7 on day 1 (CLAUDE.md «Полный День 1», step 3): when Рюта walks in after the principal has left,
    // Кайто and Рэй turn to him and Day1_Room7_Meet starts by itself. When it ends, partyFlag (heroes_unlocked)
    // brings them into the party; the main quest starts on the same flag.
    // The room is the box of this object's BoxCollider (a trigger, checked against the leader's position).
    [RequireComponent(typeof(BoxCollider))]
    public class RoomMeeting : MonoBehaviour
    {
        [SerializeField] Day1StartData data;
        [Tooltip("Who the dialogue is with (the camera frames the hero and this one)")]
        [SerializeField] GameObject speaker;
        [Tooltip("Also turn to face the hero when he walks in")]
        [SerializeField] GameObject[] listeners;

        BoxCollider box;
        bool waiting;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() => GameEvents.OnDialogueEnded += OnDialogueEnded;
        void OnDisable() => GameEvents.OnDialogueEnded -= OnDialogueEnded;

        void Update()
        {
            if (waiting || !ServiceLocator.TryGet<WorldFlags>(out var flags)) return;
            if (!flags.GetFlag(data.directorDoneFlag) || flags.GetFlag(data.partyFlag)) return;
            if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm) || fsm.Current != GameState.Break) return;
            var hero = HeroService.CurrentObject;
            if (hero == null || !box.bounds.Contains(hero.transform.position + Vector3.up * 0.5f)) return;
            if (!ServiceLocator.TryGet<IDialogueService>(out var dialogues) || dialogues.IsRunning) return;

            foreach (var l in listeners) Face(l, hero.transform.position);
            if (dialogues.StartDialogue(data.room7Meet, speaker, hero)) waiting = true;
        }

        void OnDialogueEnded(GameObject npc, GameObject hero)
        {
            if (!waiting || npc != speaker) return;
            waiting = false;
            if (ServiceLocator.TryGet<WorldFlags>(out var flags)) flags.SetFlag(data.partyFlag);
            Debug.Log("[Day1] Комната 7: Кайто и Рэй в группе.");
        }

        static void Face(GameObject who, Vector3 point)
        {
            if (who == null) return;
            Vector3 to = point - who.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) who.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }
}
