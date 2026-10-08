using System.Collections;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Story
{
    // Day 1 after the prologue (CLAUDE.md «Полный День 1», step 2), in Slice_Day1:
    // - when the arrival phase starts, Рюта stands inside the school gates and Кайто and Рэй wait in room 7
    //   (they are not in the party yet, see HeroSettings.partyUnlockFlag);
    // - the principal waits at the entrance; within directorTalkRadius his welcome (Day1_Director_Intro) starts by itself;
    // - then the screen goes black, Рюта and the principal stand at the door of room 7, Day1_Director_Room plays,
    //   and the principal walks off along his route and disappears (directorDoneFlag ends the arrival phase).
    // Every step is a WorldFlag, so a loaded save resumes at the right point.
    public class Day1Arrival : MonoBehaviour
    {
        [SerializeField] Day1StartData data;

        [Header("Start spots")]
        [SerializeField] Transform ryutaStart;
        [SerializeField] Transform kaitoStart;
        [SerializeField] Transform reiStart;

        [Header("Principal")]
        [SerializeField] GameObject director;
        [SerializeField] Transform heroAtRoom;
        [SerializeField] Transform directorAtRoom;
        [Tooltip("Points the principal walks through after room 7; he disappears at the last one")]
        [SerializeField] Transform[] directorExit;

        enum Waiting { None, Intro, Room }

        Waiting waiting;
        StoryFader fader;
        bool moving;

        void Awake() => fader = StoryFader.Create(transform, 390);

        void OnEnable()
        {
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            GameEvents.OnDialogueEnded += OnDialogueEnded;
        }

        void OnDisable()
        {
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            GameEvents.OnDialogueEnded -= OnDialogueEnded;
        }

        void Start()
        {
            if (!ServiceLocator.TryGet<WorldFlags>(out var flags)) { enabled = false; return; }
            if (ServiceLocator.TryGet<IDayCycle>(out var day) && day.CurrentPhase != null)
            {
                if (System.Array.IndexOf(data.chainPhaseIds, day.CurrentPhase.id) < 0) SkipChain(flags);
                else if (day.CurrentPhase.id == data.arrivalPhaseId) PlaceHeroes(flags);
            }
            if (flags.GetFlag(data.directorDoneFlag)) director.SetActive(false);
            else if (flags.GetFlag(data.directorMetFlag)) StartCoroutine(MoveToRoom());
        }

        void OnPhaseStarted(DayPhase phase)
        {
            if (phase != null && phase.id == data.arrivalPhaseId && ServiceLocator.TryGet<WorldFlags>(out var flags)) PlaceHeroes(flags);
        }

        void Update()
        {
            if (moving || waiting != Waiting.None || director == null || !director.activeInHierarchy) return;
            if (!ServiceLocator.TryGet<WorldFlags>(out var flags) || flags.GetFlag(data.directorMetFlag)) return;
            if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm) || fsm.Current != GameState.Break) return;

            var hero = HeroService.CurrentObject;
            if (hero == null) return;
            Vector3 d = hero.transform.position - director.transform.position;
            d.y = 0f;
            if (d.magnitude > data.directorTalkRadius) return;
            if (Talk(data.directorIntro, hero)) waiting = Waiting.Intro;
        }

        // ---------------------------------------------------------------- steps

        void PlaceHeroes(WorldFlags flags)
        {
            if (flags.GetFlag(data.placedFlag)) return;
            Place(HeroService.GetHero(HeroId.Ryuta), ryutaStart);
            Place(HeroService.GetHero(HeroId.Kaito), kaitoStart);
            Place(HeroService.GetHero(HeroId.Rei), reiStart);
            flags.SetFlag(data.placedFlag);
            Debug.Log("[Day1] Рюта у ворот, Кайто и Рэй в комнате 7.");
        }

        void OnDialogueEnded(GameObject npc, GameObject hero)
        {
            if (npc == null || npc != director) return;
            var was = waiting;
            waiting = Waiting.None;
            if (!ServiceLocator.TryGet<WorldFlags>(out var flags)) return;
            if (was == Waiting.Intro)
            {
                flags.SetFlag(data.directorMetFlag);
                StartCoroutine(MoveToRoom());
            }
            else if (was == Waiting.Room)
            {
                flags.SetFlag(data.directorDoneFlag);
                StartCoroutine(Leave());
            }
        }

        // Black screen, Рюта and the principal at the door of room 7, the principal's second line.
        IEnumerator MoveToRoom()
        {
            moving = true;
            ServiceLocator.TryGet<GameStateMachine>(out var fsm);
            fsm?.ChangeState(GameState.Cutscene);
            yield return fader.FadeTo(1f, data.fadeOutTime);
            Place(HeroService.CurrentObject, heroAtRoom);
            Place(director, directorAtRoom);
            yield return new WaitForSecondsRealtime(data.blackTime);
            fsm?.ChangeState(GameState.Break);
            yield return fader.FadeTo(0f, data.fadeInTime);
            moving = false;
            var hero = HeroService.CurrentObject;
            if (Talk(data.directorRoom, hero)) waiting = Waiting.Room;
            else if (ServiceLocator.TryGet<WorldFlags>(out var flags))
            {
                flags.SetFlag(data.directorDoneFlag);
                StartCoroutine(Leave());
            }
        }

        IEnumerator Leave()
        {
            if (directorExit != null)
                foreach (var point in directorExit)
                {
                    if (point == null) continue;
                    while (true)
                    {
                        Vector3 to = point.position - director.transform.position;
                        to.y = 0f;
                        float step = data.directorWalkSpeed * Time.deltaTime;
                        if (to.magnitude <= step) break;
                        director.transform.rotation = Quaternion.RotateTowards(director.transform.rotation,
                            Quaternion.LookRotation(to.normalized, Vector3.up), 540f * Time.deltaTime);
                        director.transform.position += to.normalized * step;
                        yield return null;
                    }
                    director.transform.position = new Vector3(point.position.x, director.transform.position.y, point.position.z);
                }
            director.SetActive(false);
            Debug.Log("[Day1] Директор ушёл.");
        }

        bool Talk(ScriptableObject dialogue, GameObject hero)
        {
            if (dialogue == null || hero == null || !ServiceLocator.TryGet<IDialogueService>(out var dialogues)) return false;
            if (dialogues.IsRunning) return false;
            return dialogues.StartDialogue(dialogue, director, hero);
        }

        // Quick Play into a later phase: the chain counts as done, so the party is complete.
        void SkipChain(WorldFlags flags)
        {
            if (flags.GetFlag(data.partyFlag)) return;
            Debug.Log("[Day1] The day starts after the arrival: the start chain counts as done.");
            foreach (var f in data.skipFlags)
                if (!string.IsNullOrEmpty(f)) flags.SetFlag(f);
        }

        static void Place(GameObject obj, Transform at)
        {
            if (obj == null || at == null) return;
            var cc = obj.GetComponent<CharacterController>();
            bool on = cc != null && cc.enabled;
            if (on) cc.enabled = false;
            obj.transform.SetPositionAndRotation(at.position, at.rotation);
            if (on) cc.enabled = true;
        }
    }
}
