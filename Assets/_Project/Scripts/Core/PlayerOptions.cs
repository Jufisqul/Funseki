using System;
using UnityEngine;

namespace Funseki.Core
{
    // The player's own options from the pause menu, kept in PlayerPrefs: master volume, mouse sensitivity,
    // dialogue text speed, windowed / fullscreen. Modules read them here (the camera multiplies its mouse look,
    // the dialogue its typing speed). The window mode shares its key with the main menu's settings.
    public static class PlayerOptions
    {
        public const float MinSensitivity = 0.25f, MaxSensitivity = 3f;
        public const float MinTextSpeed = 0.5f, MaxTextSpeed = 2.5f;

        const string VolumeKey = "options.volume";
        const string SensitivityKey = "options.mouse_sensitivity";
        const string TextSpeedKey = "options.text_speed";
        const string DisplayKey = "settings.display";

        public static event Action Changed;

        /// <summary>Master volume 0..1 (AudioListener.volume).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); AudioListener.volume = Volume; Changed?.Invoke(); }
        }

        /// <summary>Multiplier of the mouse look speed, 1 = as in PlayerSettings.</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); Changed?.Invoke(); }
        }

        /// <summary>Multiplier of the dialogue typing speed, 1 = as in DialogueSettings.</summary>
        public static float TextSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(TextSpeedKey, 1f), MinTextSpeed, MaxTextSpeed);
            set { PlayerPrefs.SetFloat(TextSpeedKey, Mathf.Clamp(value, MinTextSpeed, MaxTextSpeed)); Changed?.Invoke(); }
        }

        public static bool Fullscreen
        {
            get => (FullScreenMode)PlayerPrefs.GetInt(DisplayKey, (int)FullScreenMode.FullScreenWindow) != FullScreenMode.Windowed;
            set
            {
                var mode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                PlayerPrefs.SetInt(DisplayKey, (int)mode);
                Screen.fullScreenMode = mode;
                Changed?.Invoke();
            }
        }

        /// <summary>Applies the stored volume (called once at startup).</summary>
        public static void Apply() => AudioListener.volume = Volume;

        public static void SavePrefs() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() => Changed = null;
    }
}
