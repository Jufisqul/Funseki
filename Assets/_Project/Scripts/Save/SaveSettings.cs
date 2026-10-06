using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Save
{
    // Where the save goes, the autosave switch and the debug keys. Assets/_Project/Data/Save/SaveSettings.asset.
    [CreateAssetMenu(fileName = "SaveSettings", menuName = "Funseki/Save/Save Settings")]
    public class SaveSettings : ScriptableObject
    {
        [Tooltip("File in Application.persistentDataPath")]
        public string fileName = "funseki_save.json";

        [Tooltip("Autosave at the start of the day and of each phase, before lessons, after pranks (GDD 5.7)")]
        public bool autosave = true;

        [Header("Debug (Editor and development builds)")]
        [Tooltip("F5 saves now, F6 continues from the save (as «Продолжить» in the menu)")]
        public bool debugKeys = true;
        public Key quickSaveKey = Key.F5;
        public Key quickLoadKey = Key.F6;
    }
}
