using System;
using UnityEngine;

// Player-facing settings, persisted in PlayerPrefs. Shared by the menu and the game.
public static class GameSettings
{
    public static event Action Changed;

    public static float Music { get => Get("music", 0.8f); set => Set("music", value); }
    public static float Sfx { get => Get("sfx", 0.9f); set => Set("sfx", value); }
    public static float Vfx { get => Get("vfx", 0.9f); set => Set("vfx", value); }
    public static float Brightness { get => Get("brightness", 0.5f); set => Set("brightness", value); }

    public static FullScreenMode DisplayMode
    {
        get => (FullScreenMode)PlayerPrefs.GetInt("settings.display", (int)FullScreenMode.FullScreenWindow);
        set { PlayerPrefs.SetInt("settings.display", (int)value); Screen.fullScreenMode = value; Changed?.Invoke(); }
    }

    public static void SetResolution(Resolution r)
    {
        PlayerPrefs.SetInt("settings.resW", r.width);
        PlayerPrefs.SetInt("settings.resH", r.height);
        Screen.SetResolution(r.width, r.height, DisplayMode);
        Changed?.Invoke();
    }

    static float Get(string key, float def) => PlayerPrefs.GetFloat("settings." + key, def);

    static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat("settings." + key, Mathf.Clamp01(value));
        Changed?.Invoke();
    }
}
