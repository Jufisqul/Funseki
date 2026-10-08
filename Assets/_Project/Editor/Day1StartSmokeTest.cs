using System.IO;
using Funseki.Core;
using Funseki.Dialogue;
using Funseki.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Day1 > Smoke Test Day 1 start: plays the start of day 1 by itself, without the keyboard:
    // New Game → the prologue (skipped) → Рюта at the gates, alone in the party → walks up to the principal (the welcome
    // starts by itself) → the move to room 7 → room 7 (Кайто and Рэй join, the main quest starts) → room 8 (step 2,
    // the HUD goal changes) → save and Continue (the quest comes back on step 2) → room 9 (the quest is done, break_1).
    // Every dialogue is closed at once. Result in the Console: [Day1StartSmokeTest] PASS / FAIL.
    [InitializeOnLoad]
    public static class Day1StartSmokeTest
    {
        const string Key = "Funseki.Day1StartSmokeTest.Step";
        const float Timeout = 25f;
        static double stepStart;
        static string lastObjective;
        static readonly System.Collections.Generic.Queue<string> talks = new();

        static Day1StartSmokeTest() => EditorApplication.update += Tick;

        [MenuItem("Tools/Funseki/Day1/Smoke Test Day 1 start")]
        public static void Run()
        {
            SessionState.SetInt(Key, 1);
            CoreScenesSetup.PlayFromBootstrap();
        }

        // Same start, but stops on the file (Play keeps running) to look at the prologue screen.
        [MenuItem("Tools/Funseki/Day1/Play to the prologue (New Game, no skip)")]
        public static void PlayToPrologue()
        {
            SessionState.SetBool(HoldKey, true);
            Run();
        }

        const string HoldKey = "Funseki.Day1StartSmokeTest.Hold";

        static void Tick()
        {
            int step = SessionState.GetInt(Key, 0);
            if (step == 0 || !EditorApplication.isPlaying) return;
            // Keep the game running while the editor window is in the background.
            // (runInBackground set at runtime only: Player Settings stay as they are.)
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (stepStart == 0)
            {
                stepStart = EditorApplication.timeSinceStartup;
                GameEvents.OnObjectiveChanged += t => lastObjective = t;
            }
            if (EditorApplication.timeSinceStartup - stepStart > Timeout) { Finish($"FAIL: timed out at step {step}"); return; }

            var scene = SceneManager.GetActiveScene().name;
            ServiceLocator.TryGet<GameStateMachine>(out var fsm);
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            ServiceLocator.TryGet<IDayCycle>(out var day);
            ServiceLocator.TryGet<IQuestLog>(out var quests);
            var state = fsm?.Current ?? GameState.None;
            string phase = day?.CurrentPhase?.id;

            // One conversation at a time: the next starts once the game is back in Break.
            if (talks.Count > 0)
            {
                if (state == GameState.Break) Talk(talks.Dequeue());
                return;
            }

            switch (step)
            {
                case 1 when scene == "MainMenu" && state == GameState.MainMenu:
                    Object.FindAnyObjectByType<MainMenuController>().NewGame();
                    Next(2);
                    break;

                case 2 when scene == "Slice_Day1" && phase == "prologue":
                    var dossier = Object.FindAnyObjectByType<PrologueDossier>();
                    if (SessionState.GetBool(HoldKey, false))
                    {
                        SessionState.EraseBool(HoldKey);
                        SessionState.EraseInt(Key);
                        stepStart = 0;
                        Debug.Log("[Day1StartSmokeTest] On the prologue; Play keeps running.");
                        return;
                    }
                    if (dossier == null) { Finish("FAIL: no PrologueDossier"); return; }
                    dossier.SkipNow();
                    Next(3);
                    break;

                case 3 when phase == "arrival" && state == GameState.Break && flags.GetFlag("day1_start_placed"):
                    var ryuta = HeroService.GetHero(HeroId.Ryuta);
                    var kaito = HeroService.GetHero(HeroId.Kaito);
                    if (ryuta.transform.position.z > -55f) { Finish($"FAIL: Рюта not at the gates ({ryuta.transform.position})"); return; }
                    if (kaito.transform.position.y < 4f) { Finish($"FAIL: Кайто not in room 7 ({kaito.transform.position})"); return; }
                    if (HeroService.IsInParty(HeroId.Kaito) || HeroService.IsInParty(HeroId.Rei)) { Finish($"FAIL: Кайто/Рэй in the party before room 7 (Kaito {HeroService.IsInParty(HeroId.Kaito)}, Rei {HeroService.IsInParty(HeroId.Rei)}, heroes_unlocked {flags.GetFlag("heroes_unlocked")}, roster {ServiceLocator.IsRegistered<IHeroRoster>()}, unlock flag '{AssetDatabase.LoadAssetAtPath<Funseki.Heroes.HeroSettings>("Assets/_Project/Data/Heroes/HeroSettings.asset").partyUnlockFlag}')"); return; }
                    if (lastObjective != day.CurrentPhase.objective) { Finish($"FAIL: HUD goal '{lastObjective}'"); return; }
                    Move(ryuta, GameObject.Find("NPC_Director").transform.position + new Vector3(0f, 0.05f, -2f));
                    Next(4);
                    break;

                case 4 when state == GameState.Dialogue:
                    Close();
                    Next(5);
                    break;

                case 5 when state == GameState.Dialogue && flags.GetFlag("day1_director_met"):
                    var hero = HeroService.CurrentObject;
                    if (hero.transform.position.y < 4f) { Finish("FAIL: Рюта not moved to room 7"); return; }
                    Close();
                    Next(6);
                    break;

                case 6 when phase == "dorm" && state == GameState.Break:
                    if (!flags.GetFlag("day1_director_done")) { Finish("FAIL: day1_director_done not set"); return; }
                    Move(HeroService.CurrentObject, new Vector3(41f, 4.25f, -5.5f));
                    Next(7);
                    break;

                case 7 when state == GameState.Dialogue:
                    Close();
                    Next(8);
                    break;

                case 8 when state == GameState.Break && flags.GetFlag("heroes_unlocked"):
                    if (!HeroService.IsInParty(HeroId.Kaito) || !HeroService.IsInParty(HeroId.Rei)) { Finish("FAIL: Кайто/Рэй not in the party"); return; }
                    if (!quests.TryGet("Q_Day1_Main", out var q) || q.status != QuestStatus.Active || q.step != 0) { Finish("FAIL: Q_Day1_Main not on step 1"); return; }
                    if (lastObjective != q.objective) { Finish($"FAIL: HUD goal '{lastObjective}', expected '{q.objective}'"); return; }
                    talks.Enqueue("NPC_Takeshi"); talks.Enqueue("NPC_Yukki"); talks.Enqueue("NPC_Masumi");
                    Next(9);
                    break;

                case 9 when state == GameState.Break:
                    quests.TryGet("Q_Day1_Main", out var q2);
                    if (q2.step != 1) { Finish($"FAIL: room 8 did not close step 1 (step {q2.step})"); return; }
                    if (lastObjective != q2.objective) { Finish($"FAIL: HUD goal '{lastObjective}', expected '{q2.objective}'"); return; }
                    var save = ServiceLocator.Get<ISaveSystem>();
                    save.Save("smoke_test");
                    if (!save.PrepareContinue(out var saved)) { Finish("FAIL: no save to continue"); return; }
                    lastObjective = null;
                    SceneManager.LoadScene(saved);
                    Next(10);
                    break;

                case 10 when scene == "Slice_Day1" && phase == "dorm" && state == GameState.Break && lastObjective != null:
                    quests.TryGet("Q_Day1_Main", out var q3);
                    // The phase sets its own goal first; the quest step comes back a frame later.
                    if (lastObjective != q3.objective && EditorApplication.timeSinceStartup - stepStart < 3f) return;
                    if (q3.status != QuestStatus.Active || q3.step != 1) { Finish($"FAIL: after Continue the quest is {q3.status} step {q3.step}"); return; }
                    if (lastObjective != q3.objective) { Finish($"FAIL: after Continue HUD goal '{lastObjective}'"); return; }
                    if (!HeroService.IsInParty(HeroId.Kaito)) { Finish("FAIL: after Continue Кайто not in the party"); return; }
                    if (GameObject.Find("NPC_Director") != null) { Finish("FAIL: after Continue the principal is back"); return; }
                    talks.Enqueue("NPC_Hiro"); talks.Enqueue("NPC_Tsubaki");
                    Next(11);
                    break;

                case 11 when phase == "break_1":
                    quests.TryGet("Q_Day1_Main", out var q4);
                    if (q4.status != QuestStatus.Done) { Finish("FAIL: Q_Day1_Main not done"); return; }
                    Finish("PASS: prologue → principal → room 7 → rooms 8 and 9, quest steps, HUD goal, save/Continue on step 2");
                    break;
            }
        }

        static void Move(GameObject hero, Vector3 to)
        {
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = to;
            if (cc != null) cc.enabled = true;
        }

        static void Close()
        {
            if (ServiceLocator.TryGet<DialogueRunner>(out var runner)) runner.Stop();
        }

        // E on a classmate and the conversation closed at once.
        static void Talk(string npcName)
        {
            var npc = GameObject.Find(npcName)?.GetComponent<DialogueNpc>();
            if (npc == null) { Finish($"FAIL: no {npcName}"); return; }
            npc.Interact(HeroService.CurrentObject);
            Close();
        }

        static void Next(int step)
        {
            SessionState.SetInt(Key, step);
            stepStart = EditorApplication.timeSinceStartup;
        }

        static void Finish(string result)
        {
            if (SessionState.GetInt(Key, 0) == 0) return;
            SessionState.EraseInt(Key);
            stepStart = 0;
            talks.Clear();
            if (result.StartsWith("PASS")) Debug.Log("[Day1StartSmokeTest] " + result);
            else Debug.LogError("[Day1StartSmokeTest] " + result);
            EditorApplication.isPlaying = false;
        }
    }
}
