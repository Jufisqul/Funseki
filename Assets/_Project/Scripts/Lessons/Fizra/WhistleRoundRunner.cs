using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Lessons.Fizra
{
    /// <summary>One command as it will be given: which whistle, which direction, how long to react.</summary>
    public class PlannedCommand
    {
        public WhistleCommand command;
        public StepDirection direction;
        public float window;
        /// <summary>Pause before this command, s</summary>
        public float delay;
        public bool showCaption;

        public bool IsFalse => command.isFalse;
        /// <summary>The right answer is to press nothing (freeze, or a false command).</summary>
        public bool ExpectsNothing => command.isFalse || command.IsFreeze;
        /// <summary>Lesson-map action that answers it; null when nothing should be pressed.</summary>
        public string ExpectedAction => ExpectsNothing ? null : command.ActionFor(direction);
        public bool Counts(FizraRound round) => !IsFalse || round.falseCountsInScore;

        public override string ToString() =>
            $"{command.id}{(command.directional ? "/" + direction : "")}{(IsFalse ? " (false)" : "")} {window:0.##}s";
    }

    public enum CommandResult
    {
        /// <summary>Pressed the right thing, or rightly pressed nothing</summary>
        Right,
        /// <summary>Pressed something else (or anything on a freeze / false command)</summary>
        Wrong,
        /// <summary>Pressed nothing when something was expected</summary>
        Missed,
    }

    // Plans and plays one FizraRound, independent of the scene: give it the pressed Lesson-map action every frame
    // through Tick and listen to the events. Used by FizraMiniGame and meant for the «Контрольная» (3 commands in 10 s).
    public class WhistleRoundRunner
    {
        public FizraRound Round { get; private set; }
        public IReadOnlyList<PlannedCommand> Plan => plan;
        public PlannedCommand Current => index >= 0 && index < plan.Count && windowOpen ? plan[index] : null;
        /// <summary>0..1 of the current window that is left (for the timer bar).</summary>
        public float WindowLeft => Current == null ? 0f : Mathf.Clamp01(1f - timer / Mathf.Max(0.01f, Current.window));
        public bool IsRunning { get; private set; }
        public int Right { get; private set; }
        public int Counted { get; private set; }

        /// <summary>The whistle sounds: show the pictogram, play the sound, the teacher gestures.</summary>
        public event Action<PlannedCommand> Issued;
        /// <summary>(command, result, the action pressed or null)</summary>
        public event Action<PlannedCommand, CommandResult, string> Resolved;
        /// <summary>(right, counted)</summary>
        public event Action<int, int> Finished;

        readonly List<PlannedCommand> plan = new();
        System.Random rng;
        int index;
        float timer;
        bool windowOpen;

        // ---------------------------------------------------------------- planning

        /// <summary>Builds the sequence of a round: commandCount real commands (min..max), false ones inserted,
        /// a random direction for every directional command, pauses from gap or from totalTime.</summary>
        public static List<PlannedCommand> MakePlan(FizraRound round, System.Random rng)
        {
            var result = new List<PlannedCommand>();
            if (round == null || round.commands.Count == 0) return result;

            int count = rng.Next(round.minCommands, Mathf.Max(round.minCommands, round.maxCommands) + 1);
            WhistleCommand previous = null;
            for (int i = 0; i < count; i++)
            {
                // The new whistle of the round comes up early; no command twice in a row unless there is only one.
                WhistleCommand cmd = i == 1 && round.newCommand != null ? round.newCommand : round.commands[rng.Next(round.commands.Count)];
                if (cmd == previous && round.commands.Count > 1) cmd = round.commands[(round.commands.IndexOf(cmd) + 1) % round.commands.Count];
                previous = cmd;
                result.Add(Make(cmd, round, rng));
            }

            if (round.falseCommands.Count > 0)
                for (int f = 0; f < round.falseCount; f++)
                {
                    var cmd = round.falseCommands[rng.Next(round.falseCommands.Count)];
                    int at = round.falseInMiddle ? result.Count / 2 : rng.Next(1, result.Count + 1);
                    result.Insert(at, Make(cmd, round, rng));
                }

            float gap = round.gap;
            if (round.totalTime > 0f)
            {
                float windows = 0f;
                foreach (var p in result) windows += p.window;
                gap = Mathf.Max(0.3f, (round.totalTime - windows) / result.Count);
            }
            foreach (var p in result) p.delay = gap;
            return result;
        }

        static PlannedCommand Make(WhistleCommand cmd, FizraRound round, System.Random rng) => new()
        {
            command = cmd,
            direction = (StepDirection)rng.Next(4),
            window = cmd.window > 0f ? cmd.window : round.reactionWindow,
            showCaption = round.showCaptions,
        };

        // ---------------------------------------------------------------- playing

        public void Start(FizraRound round, int seed)
        {
            Round = round;
            rng = new System.Random(seed);
            plan.Clear();
            plan.AddRange(MakePlan(round, rng));
            index = -1;
            timer = 0f;
            windowOpen = false;
            Right = Counted = 0;
            IsRunning = plan.Count > 0;
            Debug.Log($"[Fizra] {round.name}: {string.Join(", ", plan)}");
            if (!IsRunning) Finished?.Invoke(0, 0);
        }

        public void Stop() => IsRunning = false;

        /// <param name="pressed">Lesson-map action pressed this frame, or null.</param>
        public void Tick(float dt, string pressed)
        {
            if (!IsRunning) return;
            timer += dt;

            if (!windowOpen)
            {
                int next = index + 1;
                if (next >= plan.Count) { End(); return; }
                if (timer < plan[next].delay) return;
                index = next;
                timer = 0f;
                windowOpen = true;
                Issued?.Invoke(plan[index]);
                return;
            }

            var cmd = plan[index];
            if (pressed != null)
            {
                Resolve(cmd, !cmd.ExpectsNothing && pressed == cmd.ExpectedAction ? CommandResult.Right : CommandResult.Wrong, pressed);
                return;
            }
            if (timer >= cmd.window) Resolve(cmd, cmd.ExpectsNothing ? CommandResult.Right : CommandResult.Missed, null);
        }

        /// <summary>Debug: answer the rest of the round right or wrong at once.</summary>
        public void ForceRest(bool right)
        {
            if (!IsRunning) return;
            int from = windowOpen ? index : index + 1;
            for (int i = from; i < plan.Count; i++)
            {
                var p = plan[i];
                if (!p.Counts(Round)) continue;
                Counted++;
                if (right) Right++;
            }
            windowOpen = false;
            index = plan.Count;
            End();
        }

        void Resolve(PlannedCommand cmd, CommandResult result, string pressed)
        {
            windowOpen = false;
            timer = 0f;
            if (cmd.Counts(Round))
            {
                Counted++;
                if (result == CommandResult.Right) Right++;
            }
            Resolved?.Invoke(cmd, result, pressed);
        }

        void End()
        {
            if (!IsRunning) return;
            IsRunning = false;
            Finished?.Invoke(Right, Counted);
        }
    }
}
