using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Lessons.Fizra
{
    // «Физра: Свисток» (spec in Notion: СПЕКИ → Мини-игры уроков). The PE teacher gives commands with his whistle;
    // the lead hero (Рюта, hands in his pockets) has a reaction window for each. Three rounds from FizraSettings:
    // round 1 with translations, round 2 adds a fifth whistle and drops them, round 3 is faster and the teachers'
    // kettle whistles in the middle (a false command; the students obey it anyway). Mistakes are a wobble gag,
    // the lesson goes on. Score = right answers / all commands → the outcome in LessonData.
    // «Без свистка» (prank_whistle_done): the teacher whistles through his fingers, no pictogram translations,
    // and the helper on the bench translates every command aloud, wrong 20% of the time.
    // Debug in the Editor: F7 wins the current round, F8 loses it (FizraSettings).
    public class FizraMiniGame : LessonMiniGame
    {
        const string MapName = "Lesson";

        [SerializeField] FizraSettings settings;
        [SerializeField] InputActionAsset actions;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] CinemachineCamera lessonCamera;

        [Header("Cast (children of the prefab)")]
        [SerializeField] Transform teacher;
        [SerializeField] Transform teacherBody;
        [SerializeField] Transform teacherHead;
        [Tooltip("Pivot at the shoulder; its forward is where the arm points")]
        [SerializeField] Transform teacherArm;
        [SerializeField] GameObject teacherWhistle;
        [SerializeField] GameObject teacherTear;
        [SerializeField] Transform helper;
        [Tooltip("Student roots; each has a child \"Body\" that moves")]
        [SerializeField] Transform[] students = new Transform[0];
        [SerializeField] Transform ball;
        [SerializeField] AudioSource teacherAudio;
        [Tooltip("The kettle in the teachers' room window")]
        [SerializeField] AudioSource kettleAudio;
        [Tooltip("Where the teacher points on «Позорно» (the way out of the PE area)")]
        [SerializeField] Transform exitPoint;
        [Tooltip("Stage directions: forward = towards the teacher (W), right = D")]
        [SerializeField] Transform stageAxes;

        readonly WhistleRoundRunner runner = new();
        readonly List<FizraPuppet> studentPuppets = new();
        readonly List<(FizraPuppet puppet, FizraMove move, Vector3 dir, float at)> pendingStudents = new();
        FizraPuppet hero, teacherPuppet;
        FizraView view;
        InputActionMap map;
        bool noWhistle;
        int totalRight, totalCounted;
        Vector3 ballFrom, ballTo;
        float ballT = -1f, ballTime;
        bool ballCaught;
        Quaternion armRest;
        float teacherShakeUntil, tearUntil;
        Vector3 tearStart;

        // ---------------------------------------------------------------- lesson life cycle

        protected override void OnSetup()
        {
            noWhistle = Context.GetFlag(settings.noWhistleFlag);
            if (teacherWhistle != null) teacherWhistle.SetActive(!noWhistle);
            if (teacherTear != null) { teacherTear.SetActive(false); tearStart = teacherTear.transform.localPosition; }
            if (ball != null) ball.gameObject.SetActive(false);
            if (teacherArm != null) armRest = teacherArm.localRotation;

            var model = Context.Performer != null ? FindModel(Context.Performer.transform) : null;
            hero = new FizraPuppet(model, settings.heroJumpHeight) { sitScale = settings.sitScale };
            teacherPuppet = new FizraPuppet(teacherBody, 0.15f);
            studentPuppets.Clear();
            foreach (var s in students)
                if (s != null) studentPuppets.Add(new FizraPuppet(s.Find("Body") ?? s, settings.studentJumpHeight) { sitScale = settings.sitScale });

            view = new FizraView(transform, font);
            view.SetAnchor(teacherHead != null ? teacherHead : teacher);

            if (lessonCamera != null)
            {
                lessonCamera.Priority = settings.cameraPriority;
                lessonCamera.gameObject.SetActive(true);
            }

            if (actions != null)
            {
                map = actions.FindActionMap(MapName, true);
                map.Enable();
            }

            runner.Issued += OnIssued;
            runner.Resolved += OnResolved;
            Debug.Log($"[Fizra] Setup: {(noWhistle ? "«без свистка»" : "with the whistle")}, {settings.rounds.Count} rounds.");
        }

        public override void Play() => StartCoroutine(RunRounds());

        public override GameObject GetActor(LessonRole role) => role switch
        {
            LessonRole.Teacher => teacher != null ? teacher.gameObject : null,
            LessonRole.Helper => helper != null ? helper.gameObject : null,
            LessonRole.Student => students.Length > 0 && students[Random.Range(0, students.Length)] is { } s ? s.gameObject : null,
            _ => base.GetActor(role),
        };

        public override IEnumerable<Transform> Leavers => students;

        public override void Cleanup()
        {
            runner.Stop();
            runner.Issued -= OnIssued;
            runner.Resolved -= OnResolved;
            map?.Disable();
            hero?.Reset();
            foreach (var p in studentPuppets) p.Reset();
            teacherPuppet?.Reset();
            if (lessonCamera != null) lessonCamera.gameObject.SetActive(false);
            view?.Destroy();
            view = null;
        }

        IEnumerator RunRounds()
        {
            if (noWhistle)
                foreach (var line in settings.noWhistleIntro)
                {
                    Say(line.speaker, line.text);
                    yield return Wait(line.duration);
                }

            for (int r = 0; r < settings.rounds.Count; r++)
            {
                var round = settings.rounds[r];
                if (round == null) continue;
                Context.Caption?.Show(round.title, 1.8f);
                if (!string.IsNullOrEmpty(round.startLine)) Say(LessonRole.Teacher, round.startLine);
                float start = PlayClip(teacherAudio, round.startClip != null ? round.startClip : Synth(round.startPattern, noWhistle));
                yield return Wait(Mathf.Max(1.6f, start + 0.4f));

                if (round.newCommand != null && !round.newCommand.isFalse)
                {
                    var demo = new PlannedCommand { command = round.newCommand, direction = StepDirection.Left, window = settings.newCommandDemo, showCaption = true };
                    Show(demo, true);
                    yield return Wait(settings.newCommandDemo);
                    view.HideCommand();
                    RestArm();
                    yield return Wait(0.6f);
                }

                runner.Start(round, System.Environment.TickCount + r);
                while (runner.IsRunning) yield return null;
                totalRight += runner.Right;
                totalCounted += runner.Counted;
                Debug.Log($"[Fizra] {round.title}: {runner.Right}/{runner.Counted}, total {totalRight}/{totalCounted}.");
                yield return Wait(settings.roundPause);
            }

            view.HideCommand();
            Finish(totalCounted > 0 ? totalRight / (float)totalCounted : 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (view != null) view.Tick();
            if (!Running) return;

            if (runner.IsRunning)
            {
                if (Application.isEditor) DebugKeys();
                runner.Tick(dt, Pressed());
                if (runner.Current != null && view != null) view.SetTimer(runner.WindowLeft);
            }

            hero?.Tick(dt);
            teacherPuppet?.Tick(dt);
            foreach (var p in studentPuppets) p.Tick(dt);
            for (int i = pendingStudents.Count - 1; i >= 0; i--)
            {
                var p = pendingStudents[i];
                if (Time.time < p.at) continue;
                if (p.move == FizraMove.None) p.puppet.Mistake(settings.moveTime * 1.5f, settings.mistakeTilt);
                else p.puppet.Play(p.move, p.dir, settings.moveTime);
                pendingStudents.RemoveAt(i);
            }
            TickBall(dt);
            TickTeacher();
        }

        void DebugKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[settings.winRoundKey].wasPressedThisFrame) { Debug.Log("[Fizra] Debug: round won."); runner.ForceRest(true); view?.HideCommand(); }
            else if (kb[settings.loseRoundKey].wasPressedThisFrame) { Debug.Log("[Fizra] Debug: round lost."); runner.ForceRest(false); view?.HideCommand(); }
        }

        string Pressed()
        {
            if (map == null) return null;
            foreach (var a in map.actions)
                if (a.WasPressedThisFrame()) return a.name;
            return null;
        }

        // ---------------------------------------------------------------- commands

        void OnIssued(PlannedCommand pc)
        {
            if (pc.IsFalse)
            {
                // The kettle: not the teacher. Everybody else obeys it anyway.
                PlayClip(kettleAudio != null ? kettleAudio : teacherAudio, Synth(pc.command.synthPattern, false));
                if (teacher != null && kettleAudio != null) LookAt(teacher, kettleAudio.transform.position);
                foreach (var s in studentPuppets)
                    pendingStudents.Add((s, pc.command.falseMoveOfStudents, Vector3.zero, Time.time + Random.Range(0.25f, 0.6f)));
                return;
            }
            Show(pc, pc.showCaption);
            StudentsObey(pc);
            if (pc.command.move == FizraMove.Catch) ThrowBall(pc.window);
        }

        // Pictogram, sound and gesture; in «без свистка» the helper translates instead of the caption.
        void Show(PlannedCommand pc, bool withCaption)
        {
            var cmd = pc.command;
            string translation = Translate(cmd, pc.direction);
            if (noWhistle)
            {
                view.ShowCommand(null, settings.noWhistleGlyph, null);
                PlayClip(teacherAudio, cmd.fingerClip != null ? cmd.fingerClip : Synth(cmd.synthPattern, true));
                Say(LessonRole.Helper, HelperTranslation(pc));
            }
            else
            {
                view.ShowCommand(cmd.icon, cmd.glyph + (cmd.directional ? " " + Arrow(pc.direction) : ""), withCaption ? translation : null);
                PlayClip(teacherAudio, cmd.whistleClip != null ? cmd.whistleClip : Synth(cmd.synthPattern, false));
            }
            if (teacher != null && stageAxes != null) teacher.rotation = Quaternion.LookRotation(-stageAxes.forward, Vector3.up);
            PointArm(cmd.move, pc.direction);
        }

        string HelperTranslation(PlannedCommand pc)
        {
            var cmd = pc.command;
            var dir = pc.direction;
            var round = runner.Round != null ? runner.Round : settings.rounds.Count > 0 ? settings.rounds[0] : null;
            if (round != null && round.commands.Count > 1 && Random.value < settings.helperWrongChance)
            {
                var others = round.commands.FindAll(c => c != cmd && !c.isFalse);
                if (others.Count > 0) cmd = others[Random.Range(0, others.Count)];
                dir = (StepDirection)Random.Range(0, 4);
                Debug.Log($"[Fizra] The helper translates {pc.command.id} wrong as {cmd.id}.");
            }
            var formats = settings.helperTranslateFormats;
            string text = Translate(cmd, dir);
            return formats != null && formats.Length > 0 ? string.Format(formats[Random.Range(0, formats.Length)], text) : text;
        }

        static string Translate(WhistleCommand cmd, StepDirection dir) =>
            cmd.caption != null && cmd.caption.Contains("{0}") ? string.Format(cmd.caption, DirectionName(dir)) : cmd.caption;

        static string DirectionName(StepDirection d) => d switch
        {
            StepDirection.Up => "вперёд",
            StepDirection.Down => "назад",
            StepDirection.Left => "влево",
            _ => "вправо",
        };

        static string Arrow(StepDirection d) => d switch
        {
            StepDirection.Up => "^",
            StepDirection.Down => "v",
            StepDirection.Left => "<",
            _ => ">",
        };

        void StudentsObey(PlannedCommand pc)
        {
            foreach (var s in studentPuppets)
            {
                FizraMove move = pc.command.move;
                Vector3 dir = Vector3.zero;
                if (Random.value < settings.studentErrorChance) move = Random.value < 0.5f ? FizraMove.None : (FizraMove)Random.Range(1, 4);
                else if (pc.command.IsFreeze) continue;
                if (move == FizraMove.Step) dir = LocalDir(s.Body, WorldDir(pc.direction)) * settings.stepDistance;
                pendingStudents.Add((s, move, dir, Time.time + Random.Range(0.3f, 0.8f)));
            }
        }

        void OnResolved(PlannedCommand pc, CommandResult result, string pressed)
        {
            view.HideCommand();
            RestArm();
            if (pc.command.move == FizraMove.Catch) ballCaught = result == CommandResult.Right;

            switch (result)
            {
                case CommandResult.Right:
                    view.ShowFeedback(settings.rightText, new Color(0.45f, 0.95f, 0.4f));
                    if (!pc.ExpectsNothing && hero != null && hero.Body != null)
                        hero.Play(pc.command.move, LocalDir(hero.Body, WorldDir(pc.direction)) * settings.stepDistance, settings.moveTime);
                    break;
                default:
                    view.ShowFeedback(result == CommandResult.Missed ? settings.missedText : settings.wrongText, new Color(1f, 0.45f, 0.35f));
                    hero?.Mistake(settings.moveTime * 1.6f, settings.mistakeTilt);
                    if (Random.value < settings.helperTeaseChance) Say(LessonRole.Helper, Pick(settings.helperTeaseLines));
                    else if (Random.value < settings.heroMistakeLineChance) Say(LessonRole.LeadHero, Pick(settings.heroMistakeLines));
                    break;
            }

            if (pc.IsFalse)
            {
                Say(LessonRole.Teacher, Pick(settings.teacherFalseLines));
                Say(LessonRole.Student, Pick(settings.studentFalseLines));
            }
            if (settings.showScore) view.SetScore(string.Format(settings.scoreFormat, totalRight + runner.Right, totalCounted + runner.Counted));
        }

        // ---------------------------------------------------------------- outcome gags

        public override void PlayGag(string gag, LessonOutcome outcome)
        {
            view?.HideCommand();
            view?.SetScore(null);
            RestArm();
            switch (gag)
            {
                case "anthem":
                    PlayClip(teacherAudio, Synth(settings.anthemNotes, noWhistle));
                    Tear();
                    break;
                case "voice_anthem":
                    // He sings with his own voice, for the first time in years: the class faints.
                    Tear();
                    foreach (var s in studentPuppets) s.FallOver();
                    break;
                case "nod":
                    PlayClip(teacherAudio, Synth(settings.approvePattern, noWhistle));
                    teacherPuppet?.Play(FizraMove.Jump, Vector3.zero, 0.5f);
                    break;
                case "point_exit":
                    PlayClip(teacherAudio, Synth(settings.angryPattern, noWhistle));
                    if (teacherArm != null && exitPoint != null)
                        teacherArm.rotation = Quaternion.LookRotation((exitPoint.position - teacherArm.position).normalized, Vector3.up);
                    teacherShakeUntil = Time.time + 2.5f;
                    break;
            }
        }

        void Tear()
        {
            if (teacherTear == null) return;
            teacherTear.transform.localPosition = tearStart;
            teacherTear.SetActive(true);
            tearUntil = Time.time + 3f;
        }

        void TickTeacher()
        {
            if (teacherTear != null && teacherTear.activeSelf)
            {
                teacherTear.transform.localPosition -= Vector3.up * 0.25f * Time.deltaTime;
                if (Time.time > tearUntil) teacherTear.SetActive(false);
            }
            if (teacherBody != null && Time.time < teacherShakeUntil)
                teacherBody.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 40f) * 4f);
            else if (teacherBody != null && teacherShakeUntil > 0f)
            {
                teacherBody.localRotation = Quaternion.identity;
                teacherShakeUntil = 0f;
            }
        }

        // ---------------------------------------------------------------- gesture and ball

        void PointArm(FizraMove move, StepDirection dir)
        {
            if (teacherArm == null) return;
            Vector3 world = move switch
            {
                FizraMove.Jump => Vector3.up,
                FizraMove.Sit => Vector3.down + (teacher != null ? teacher.forward * 0.4f : Vector3.zero),
                FizraMove.Step => WorldDir(dir) + Vector3.up * 0.2f,
                FizraMove.Catch => teacher != null ? teacher.forward : Vector3.forward,
                _ => (teacher != null ? teacher.right : Vector3.right) + Vector3.up * 0.1f,   // freeze: arm out, palm "stop"
            };
            world.Normalize();
            Vector3 upHint = Mathf.Abs(Vector3.Dot(world, Vector3.up)) > 0.9f ? (teacher != null ? teacher.forward : Vector3.forward) : Vector3.up;
            teacherArm.rotation = Quaternion.LookRotation(world, upHint);
        }

        void RestArm()
        {
            if (teacherArm != null) teacherArm.localRotation = armRest;
        }

        Vector3 WorldDir(StepDirection d)
        {
            var axes = stageAxes != null ? stageAxes : transform;
            Vector3 fwd = Vector3.ProjectOnPlane(axes.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(axes.right, Vector3.up).normalized;
            return d switch
            {
                StepDirection.Up => fwd,
                StepDirection.Down => -fwd,
                StepDirection.Left => -right,
                _ => right,
            };
        }

        static Vector3 LocalDir(Transform body, Vector3 world) =>
            body != null && body.parent != null ? body.parent.InverseTransformDirection(world) : world;

        void ThrowBall(float window)
        {
            if (ball == null || teacherArm == null || Context.Performer == null) return;
            ballFrom = teacherArm.position + teacherArm.forward * 0.4f;
            ballTo = Context.Performer.transform.position + Vector3.up * 1.2f;
            ballTime = Mathf.Max(0.2f, window * settings.ballFlightPart);
            ballT = 0f;
            ballCaught = false;
            ball.position = ballFrom;
            ball.gameObject.SetActive(true);
        }

        void TickBall(float dt)
        {
            if (ball == null || ballT < 0f) return;
            ballT += dt;
            float k = ballT / ballTime;
            if (k <= 1f)
            {
                ball.position = Vector3.Lerp(ballFrom, ballTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                return;
            }
            // Caught: drops at his feet. Missed: bonk, bounces off over his head.
            float after = ballT - ballTime;
            Vector3 away = (ballTo - ballFrom).normalized;
            ball.position = ballCaught
                ? ballTo + Vector3.down * Mathf.Min(1.1f, after * 3f)
                : ballTo + away * after * 3f + Vector3.up * (after * 4f - after * after * 9.8f);
            if (after > 1f) { ballT = -1f; ball.gameObject.SetActive(false); }
        }

        // ---------------------------------------------------------------- helpers

        AudioClip Synth(string pattern, bool finger) =>
            WhistleSynth.Make(pattern, finger ? settings.fingerHz : settings.whistleHz, finger);

        float PlayClip(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return 0f;
            source.PlayOneShot(clip, settings.whistleVolume);
            return clip.length;
        }

        static void LookAt(Transform who, Vector3 point)
        {
            Vector3 to = Vector3.ProjectOnPlane(point - who.position, Vector3.up);
            if (to.sqrMagnitude > 0.01f) who.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        static string Pick(string[] lines) => lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : null;

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (Running) t += Time.deltaTime;
                yield return null;
            }
        }

        // The hero's visual: the "Model" child made by the slice builder, else the first child with an Animator.
        static Transform FindModel(Transform hero)
        {
            var model = hero.Find("Model");
            if (model != null) return model;
            var animator = hero.GetComponentInChildren<Animator>();
            return animator != null && animator.transform != hero ? animator.transform : null;
        }
    }
}
