using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// "Настройки": values of the Изображение / Аудио pages, wired to GameSettings.
public class SettingsPanel : MonoBehaviour
{
    public TMP_Text resolutionValue, displayValue;
    public Slider brightness, music, sfx, vfx;
    public Volume volume;

    static readonly FullScreenMode[] Modes = { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen };
    static readonly string[] ModeNames = { "Окно", "Без рамки", "Полный экран" };

    readonly List<Resolution> resolutions = new List<Resolution>();
    int resIndex, modeIndex;

    void Awake()
    {
        foreach (var r in Screen.resolutions)
            if (!resolutions.Exists(x => x.width == r.width && x.height == r.height)) resolutions.Add(r);
        if (resolutions.Count == 0) resolutions.Add(Screen.currentResolution);
        resIndex = resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        if (resIndex < 0) resIndex = resolutions.Count - 1;
        modeIndex = Mathf.Max(0, System.Array.IndexOf(Modes, Screen.fullScreenMode));

        Bind(brightness, GameSettings.Brightness, v => { GameSettings.Brightness = v; ApplyBrightness(); });
        Bind(music, GameSettings.Music, v => GameSettings.Music = v);
        Bind(sfx, GameSettings.Sfx, v => GameSettings.Sfx = v);
        Bind(vfx, GameSettings.Vfx, v => GameSettings.Vfx = v);
        ApplyBrightness();
        Refresh();
    }

    static void Bind(Slider s, float value, UnityEngine.Events.UnityAction<float> onChange)
    {
        if (!s) return;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(onChange);
    }

    public void StepResolution(int dir)
    {
        resIndex = (resIndex + dir + resolutions.Count) % resolutions.Count;
        GameSettings.SetResolution(resolutions[resIndex]);
        MenuAudio.Click();
        Refresh();
    }

    public void StepDisplayMode(int dir)
    {
        modeIndex = (modeIndex + dir + Modes.Length) % Modes.Length;
        GameSettings.DisplayMode = Modes[modeIndex];
        MenuAudio.Click();
        Refresh();
    }

    void Refresh()
    {
        var r = resolutions[resIndex];
        resolutionValue.text = $"{r.width} × {r.height}";
        displayValue.text = ModeNames[modeIndex];
    }

    void ApplyBrightness()
    {
        if (volume && volume.profile.TryGet(out ColorAdjustments ca))
        {
            ca.postExposure.overrideState = true;
            ca.postExposure.value = Mathf.Lerp(-0.8f, 0.8f, GameSettings.Brightness);
        }
    }
}
