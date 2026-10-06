using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // The PE teacher (физрук) in the slice prank «Украсть свисток». On the teacher's root next to its INpc (NpcActor,
    // whose view cone follows this transform). During the routine's phase: every repeatEvery seconds he walks from his
    // post to the vending machine, puts the whistle on the bench (the PrankTrigger becomes available) and fights the
    // machine for fightDuration, glancing at the bench now and then. Кайто's kick on the machine (KickTarget) makes him
    // turn away for distractDuration. If he catches the hero (OnCaught with him as the witness) he takes the whistle back.
    // When the whistle is stolen (OnPrankDone) he stays for the world reaction and never goes to the machine again.
    public class WhistleTeacher : MonoBehaviour
    {
        enum State { AtPost, Going, Fighting, Returning, Panicking }

        [SerializeField] WhistleRoutineData data;
        [Tooltip("Where he stands between the walks; forward = where he looks")]
        [SerializeField] Transform post;
        [Tooltip("Where he stands to fight the machine; forward = towards the machine")]
        [SerializeField] Transform fightSpot;
        [Tooltip("The whistle on the bench")]
        [SerializeField] PrankTrigger benchWhistle;
        [Tooltip("The whistle on his neck, shown while he has it")]
        [SerializeField] GameObject handWhistle;
        [Tooltip("The machine's KickTarget")]
        [SerializeField] KickTarget machine;

        State state = State.AtPost;
        float nextTripAt;
        float fightEndsAt, nextGlanceAt, glanceUntil, distractedUntil, nextLineAt, panicUntil;
        bool stolen;

        void OnEnable()
        {
            GameEvents.OnKicked += OnKicked;
            GameEvents.OnCaught += OnCaught;
            GameEvents.OnPrankDone += OnPrankDone;
            GameEvents.OnPhaseStarted += OnPhaseStarted;
        }

        void OnDisable()
        {
            GameEvents.OnKicked -= OnKicked;
            GameEvents.OnCaught -= OnCaught;
            GameEvents.OnPrankDone -= OnPrankDone;
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
        }

        void Start()
        {
            stolen = ServiceLocator.TryGet<WorldFlags>(out var flags) && data.prank != null && flags.GetFlag(data.prank.DoneFlag);
            if (handWhistle != null) handWhistle.SetActive(!stolen);
            if (benchWhistle != null) benchWhistle.SetAvailable(false);
            if (post != null) transform.SetPositionAndRotation(post.position, post.rotation);
            nextTripAt = Time.time + data.firstTripDelay;
        }

        void OnPhaseStarted(DayPhase phase)
        {
            if (phase != null && phase.id == data.phaseId) nextTripAt = Time.time + data.firstTripDelay;
        }

        void Update()
        {
            // Everything waits during a conversation, the pause, a lesson or «Поймали».
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && fsm.Current != GameState.Break) return;
            float now = Time.time;

            switch (state)
            {
                case State.AtPost:
                    if (post != null) Turn(post.forward);
                    if (!stolen && RoutineActive() && now >= nextTripAt)
                    {
                        nextTripAt = now + data.repeatEvery;
                        state = State.Going;
                        Say(PrankData.Pick(data.goLines));
                    }
                    break;

                case State.Going:
                    if (!RoutineActive()) { state = State.Returning; break; }
                    if (MoveTo(fightSpot.position)) StartFight(now);
                    break;

                case State.Fighting:
                    Fight(now);
                    break;

                case State.Panicking:
                    if (now >= panicUntil) state = State.Returning;
                    break;

                case State.Returning:
                    if (MoveTo(post.position)) state = State.AtPost;
                    break;
            }
        }

        // ---------------------------------------------------------------- the fight with the machine

        void StartFight(float now)
        {
            state = State.Fighting;
            fightEndsAt = now + data.fightDuration;
            nextGlanceAt = now + Random.Range(data.glanceEveryMin, data.glanceEveryMax);
            glanceUntil = distractedUntil = -1f;
            nextLineAt = now + 1f;
            if (handWhistle != null) handWhistle.SetActive(false);
            if (benchWhistle != null) benchWhistle.SetAvailable(true);
            Debug.Log($"[Whistle] {name} fights the machine for {data.fightDuration:0} s; the whistle is on the bench.", this);
        }

        void Fight(float now)
        {
            if (now >= fightEndsAt || !RoutineActive())
            {
                TakeWhistle();
                Say(PrankData.Pick(data.giveUpLines));
                state = State.Returning;
                return;
            }

            Vector3 look;
            if (now < distractedUntil) look = -fightSpot.forward; // turned away from the machine and the bench
            else
            {
                if (now >= nextGlanceAt)
                {
                    glanceUntil = now + data.glanceDuration;
                    nextGlanceAt = glanceUntil + Random.Range(data.glanceEveryMin, data.glanceEveryMax);
                }
                look = now < glanceUntil && benchWhistle != null
                    ? benchWhistle.transform.position - transform.position
                    : fightSpot.forward;
                if (now >= nextLineAt)
                {
                    nextLineAt = now + data.fightLineEvery;
                    Say(PrankData.Pick(data.fightLines));
                }
            }
            Turn(look);
        }

        void TakeWhistle()
        {
            if (benchWhistle != null) benchWhistle.SetAvailable(false);
            if (handWhistle != null) handWhistle.SetActive(!stolen);
        }

        // ---------------------------------------------------------------- events

        void OnKicked(GameObject target, GameObject hero)
        {
            if (state != State.Fighting || machine == null || target != machine.gameObject) return;
            distractedUntil = Time.time + data.distractDuration;
            glanceUntil = -1f;
            nextGlanceAt = distractedUntil + Random.Range(data.glanceEveryMin, data.glanceEveryMax);
            Say(PrankData.Pick(data.distractLines));
            Debug.Log($"[Whistle] {name} turned away for {data.distractDuration:0} s.", this);
        }

        void OnCaught(GameObject hero, GameObject witness, ScriptableObject scene)
        {
            if (witness != gameObject) return;
            // He takes the whistle back and chases the hero with it; after the punishment he goes back to his post.
            if (state == State.Fighting || state == State.Going)
            {
                TakeWhistle();
                state = State.Returning;
            }
        }

        void OnPrankDone(IPrank prank, Vector3 position)
        {
            if (data.prank == null || !ReferenceEquals(prank, data.prank)) return;
            stolen = true;
            if (handWhistle != null) handWhistle.SetActive(false);
            state = State.Panicking;
            panicUntil = Time.time + data.panicTime;
        }

        // ---------------------------------------------------------------- helpers

        bool RoutineActive()
        {
            if (!ServiceLocator.TryGet<IDayCycle>(out var day)) return true; // test scenes without a day
            return !day.WaitingForLesson && day.CurrentPhase != null && day.CurrentPhase.id == data.phaseId;
        }

        // True when arrived.
        bool MoveTo(Vector3 target)
        {
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            if (to.magnitude < 0.05f)
            {
                transform.position = new Vector3(target.x, pos.y, target.z);
                return true;
            }
            Turn(to);
            Vector3 next = Vector3.MoveTowards(pos, new Vector3(target.x, pos.y, target.z), data.walkSpeed * Time.deltaTime);
            transform.position = next;
            return false;
        }

        void Turn(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, data.turnSpeed * Time.deltaTime);
        }

        void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(gameObject, text);
        }
    }
}
