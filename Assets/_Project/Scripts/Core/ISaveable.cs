using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Core
{
    // A piece of game state that goes into the save file as one JSON string under SaveKey.
    // Services register themselves in SaveRegistry (Awake) and unregister on destroy; Funseki.Save writes them all.
    public interface ISaveable
    {
        /// <summary>Unique snake_case key in the save file: world, inventory, day.</summary>
        string SaveKey { get; }
        string ToJson();
        void LoadJson(string json);
    }

    public static class SaveRegistry
    {
        static readonly Dictionary<string, ISaveable> items = new();
        // Parts of a loaded save whose owners live in the scene that is being loaded (the day, the heroes).
        // Handed to each owner the moment it registers, i.e. in its Awake, before its Start.
        static readonly Dictionary<string, string> pending = new();

        public static IEnumerable<ISaveable> All => items.Values;

        /// <summary>A loaded save is waiting for scene objects to register (Continue from the menu).</summary>
        public static bool IsRestoring => pending.Count > 0;

        public static void Register(ISaveable saveable)
        {
            if (saveable == null || string.IsNullOrEmpty(saveable.SaveKey)) return;
            items[saveable.SaveKey] = saveable;
            if (pending.TryGetValue(saveable.SaveKey, out var json))
            {
                pending.Remove(saveable.SaveKey);
                saveable.LoadJson(json);
            }
        }

        public static void Unregister(ISaveable saveable)
        {
            if (saveable != null && items.TryGetValue(saveable.SaveKey, out var current) && current == saveable)
                items.Remove(saveable.SaveKey);
        }

        public static bool TryGet(string key, out ISaveable saveable) => items.TryGetValue(key, out saveable);

        /// <summary>Keeps this part until its owner registers.</summary>
        public static void SetPending(string key, string json)
        {
            if (!string.IsNullOrEmpty(key)) pending[key] = json;
        }

        public static void ClearPending() => pending.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            items.Clear();
            pending.Clear();
        }
    }

    // The save system (Funseki.Save.SaveService under [Bootstrap]), via ServiceLocator. The main menu and the pause menu use it.
    public interface ISaveSystem
    {
        bool HasSave { get; }
        /// <summary>Writes the game now (autosave).</summary>
        void Save(string reason);
        /// <summary>Resets flags, inventory and journal to a fresh game. The caller loads the first game scene.</summary>
        void ResetForNewGame();
        /// <summary>Reads the save into the services; the parts that live in a scene wait for it.
        /// Returns the scene to load (the caller loads it), false if there is no readable save.</summary>
        bool PrepareContinue(out string scene);
    }
}
