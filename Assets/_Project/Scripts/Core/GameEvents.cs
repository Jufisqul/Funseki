using System;
using UnityEngine;

namespace Funseki.Core
{
    // The only bridge between modules: a module raises an event here, others subscribe.
    // Modules never reference each other directly. Add new events to this file, grouped by module.
    public static class GameEvents
    {
        // ---- Core
        /// <summary>(previous, current)</summary>
        public static event Action<GameState, GameState> OnGameStateChanged;
        /// <summary>(flag id, new value)</summary>
        public static event Action<string, bool> OnWorldFlagChanged;
        /// <summary>(counter id, new value)</summary>
        public static event Action<string, int> OnWorldCounterChanged;

        public static void RaiseGameStateChanged(GameState previous, GameState current) => OnGameStateChanged?.Invoke(previous, current);
        public static void RaiseWorldFlagChanged(string id, bool value) => OnWorldFlagChanged?.Invoke(id, value);
        public static void RaiseWorldCounterChanged(string id, int value) => OnWorldCounterChanged?.Invoke(id, value);

        // Static events survive Play sessions when domain reload is off; drop stale subscribers.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            OnGameStateChanged = null;
            OnWorldFlagChanged = null;
            OnWorldCounterChanged = null;
        }
    }
}
