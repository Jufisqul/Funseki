using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Funseki.Save
{
    // The autosave (GDD 5.7): writes every ISaveable from SaveRegistry into one JSON file in persistentDataPath,
    // with the format version, the scene and the reason. Parts: "world" (WorldFlags, which also hold the state of every
    // interactable object), "inventory", "journal" (services under [Bootstrap]), "day" (DayCycleDirector: phase and its
    // timers) and "heroes" (HeroParty: who is in control and where everyone stands) from the day scene.
    // Autosaves: at the start of the day and of every next phase (the day schedule), before each lesson
    // (LessonDirector asks through OnAutosaveRequested), after each prank and after a story scene (the next phase starts).
    // Continue: the [Bootstrap] parts are loaded at once, the scene parts wait in SaveRegistry until their owners wake up
    // in the loaded scene. Implements ISaveSystem for the menus. Debug: F5 save, F6 continue from the save (SaveSettings).
    [DefaultExecutionOrder(-900)]
    public class SaveService : MonoBehaviour, ISaveSystem
    {
        /// <summary>Version of the file layout. Raise it when a part changes incompatibly; newer files are refused.</summary>
        public const int FormatVersion = 1;

        [SerializeField] SaveSettings settings;

        [Serializable]
        struct Entry { public string key; public string json; }

        [Serializable]
        class SaveFile
        {
            public int version;
            public string savedAt;
            public string reason;
            public string scene;
            public List<Entry> entries = new();
        }

        // WorldFlags lives in Core and can't register itself; this adapter does it.
        class WorldSaveable : ISaveable
        {
            public string SaveKey => "world";
            public string ToJson() => ServiceLocator.TryGet<WorldFlags>(out var f) ? f.ToJson() : "{}";
            public void LoadJson(string json)
            {
                if (ServiceLocator.TryGet<WorldFlags>(out var f)) f.LoadJson(json);
            }
        }

        readonly WorldSaveable world = new();
        // A fresh game: the [Bootstrap] parts as they were before anything happened.
        readonly Dictionary<string, string> defaults = new();
        string queuedReason;
        bool owner;

        public string FilePath => Path.Combine(Application.persistentDataPath, settings.fileName);

        public bool HasSave => ReadFile(out _);

        void Awake()
        {
            if (ServiceLocator.IsRegistered<SaveService>()) { Destroy(gameObject); return; }
            owner = true;
            ServiceLocator.Register(this);
            ServiceLocator.Register<ISaveSystem>(this);
            SaveRegistry.Register(world);
        }

        void Start()
        {
            if (!owner) return;
            foreach (var s in SaveRegistry.All)
                if (IsPersistent(s)) defaults[s.SaveKey] = s.ToJson();
        }

        void OnEnable()
        {
            GameEvents.OnAutosaveRequested += OnAutosaveRequested;
            GameEvents.OnPrankDone += OnPrankDone;
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.OnAutosaveRequested -= OnAutosaveRequested;
            GameEvents.OnPrankDone -= OnPrankDone;
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (!owner) return;
            SaveRegistry.Unregister(world);
            if (ServiceLocator.TryGet<SaveService>(out var s) && s == this) ServiceLocator.Unregister<SaveService>();
            if (ServiceLocator.TryGet<ISaveSystem>(out var i) && ReferenceEquals(i, this)) ServiceLocator.Unregister<ISaveSystem>();
        }

        // ---------------------------------------------------------------- autosave triggers

        void OnAutosaveRequested(string reason) => Queue(reason);

        void OnPrankDone(IPrank prank, Vector3 position) => Queue($"prank_{prank?.Id}");

        // The start of the day, the break after a story scene or a lesson. A lesson saves itself through OnAutosaveRequested.
        void OnPhaseStarted(DayPhase phase)
        {
            if (phase != null && phase.type != DayPhaseType.Lesson) Queue($"phase_{phase.id}");
        }

        // Saves at the end of the frame, once, so every listener of the same event has already updated its state.
        void Queue(string reason)
        {
            // Editor Quick Play never overwrites the real save.
            if (!owner || !settings.autosave || QuickPlay.Active) return;
            bool first = queuedReason == null;
            queuedReason = first ? reason : $"{queuedReason}+{reason}";
            if (first) StartCoroutine(SaveAtEndOfFrame());
        }

        IEnumerator SaveAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            string reason = queuedReason;
            queuedReason = null;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)
                && (fsm.Current == GameState.MainMenu || fsm.Current == GameState.None || fsm.Current == GameState.SliceEnd))
                yield break;
            Save(reason);
        }

        // ---------------------------------------------------------------- ISaveSystem

        public void Save(string reason)
        {
            var file = new SaveFile
            {
                version = FormatVersion,
                savedAt = DateTime.Now.ToString("s"),
                reason = reason,
                scene = SceneManager.GetActiveScene().name,
            };
            foreach (var s in SaveRegistry.All) file.entries.Add(new Entry { key = s.SaveKey, json = s.ToJson() });
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Could not write {FilePath}: {e.Message}");
                return;
            }
            Debug.Log($"[Save] Saved ({reason}): {file.entries.Count} part(s) to {FilePath}");
            GameEvents.RaiseGameSaved(FilePath);
        }

        public void ResetForNewGame()
        {
            SaveRegistry.ClearPending();
            foreach (var kv in defaults)
                if (SaveRegistry.TryGet(kv.Key, out var s)) s.LoadJson(kv.Value);
            Debug.Log($"[Save] New game: {defaults.Count} part(s) reset.");
        }

        public bool PrepareContinue(out string scene)
        {
            scene = null;
            if (!ReadFile(out var file))
            {
                Debug.LogWarning($"[Save] No readable save at {FilePath}");
                return false;
            }
            SaveRegistry.ClearPending();
            ResetForNewGame();
            int now = 0, later = 0;
            foreach (var e in file.entries)
            {
                if (SaveRegistry.TryGet(e.key, out var s) && IsPersistent(s)) { s.LoadJson(e.json); now++; }
                else { SaveRegistry.SetPending(e.key, e.json); later++; }
            }
            scene = file.scene;
            Debug.Log($"[Save] Continue from {file.savedAt} ({file.reason}) in {file.scene}: {now} part(s) loaded, {later} wait for the scene.");
            GameEvents.RaiseGameLoaded(FilePath);
            return true;
        }

        // ---------------------------------------------------------------- internals

        bool ReadFile(out SaveFile file)
        {
            file = null;
            if (settings == null || !File.Exists(FilePath)) return false;
            try
            {
                file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] {FilePath} is unreadable: {e.Message}");
                return false;
            }
            if (file == null || string.IsNullOrEmpty(file.scene)) return false;
            if (file.version > FormatVersion)
            {
                Debug.LogWarning($"[Save] {FilePath} has format {file.version}, this build reads up to {FormatVersion}.");
                return false;
            }
            return true;
        }

        // Lives through scene loads: the WorldFlags adapter and the services under [Bootstrap].
        static bool IsPersistent(ISaveable s) =>
            !(s is Component c) || c == null || c.gameObject.scene.name == "DontDestroyOnLoad";

        // Parts nobody in the new scene asked for are dropped, so the next save starts clean.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && SaveRegistry.IsRestoring) StartCoroutine(DropLeftovers());
        }

        IEnumerator DropLeftovers()
        {
            yield return null;
            yield return null;
            if (!SaveRegistry.IsRestoring) yield break;
            Debug.LogWarning("[Save] Some saved parts have no owner in this scene and were skipped.");
            SaveRegistry.ClearPending();
        }

        void Update()
        {
            if (!Debug.isDebugBuild || !settings.debugKeys) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[settings.quickSaveKey].wasPressedThisFrame) Save("debug");
            else if (kb[settings.quickLoadKey].wasPressedThisFrame && PrepareContinue(out var scene))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(scene);
            }
        }
    }
}
