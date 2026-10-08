using UnityEngine;

namespace Funseki.Core
{
    // Editor-only quick test start (Tools > Funseki > Quick Play): Bootstrap opens a scene directly,
    // the day starts at a chosen phase, the leader can be dropped at a point, and autosaves are off
    // so the real save is not overwritten. The editor writes these keys into SessionState before Play
    // and clears them when Play ends; in a build everything here is inactive.
    public static class QuickPlay
    {
        public const string SceneKey = "Funseki.QuickPlay.Scene";
        public const string PhaseKey = "Funseki.QuickPlay.Phase";
        public const string SpawnKey = "Funseki.QuickPlay.Spawn";

        public static bool Active => !string.IsNullOrEmpty(Get(SceneKey));
        public static string Scene => Get(SceneKey);
        public static string Phase => Get(PhaseKey);

        public static bool TryGetSpawn(out Vector3 position)
        {
            position = default;
            var s = Get(SpawnKey);
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(';');
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return p.Length == 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, c, out position.x)
                                 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, c, out position.y)
                                 && float.TryParse(p[2], System.Globalization.NumberStyles.Float, c, out position.z);
        }

        static string Get(string key)
        {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetString(key, "");
#else
            return "";
#endif
        }
    }
}
