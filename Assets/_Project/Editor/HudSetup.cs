using System.Collections.Generic;
using System.IO;
using Funseki.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > UI > Setup HUD in Slice_Day1 (GDD 8.2):
    // - data: HudTheme (fonts, colors, icons) and HudSettings (visibility, texts, first-time hints) in Data/UI —
    //   created once and kept, so designer and artist edits survive;
    // - Slice_Day1: the root "HUD" with HudView (rebuilt every time). Other roots are not touched.
    public static class HudSetup
    {
        const string DataDir = "Assets/_Project/Data/UI";
        const string ThemePath = DataDir + "/HudTheme.asset";
        const string SettingsPath = DataDir + "/HudSettings.asset";
        const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        const string TitleFontPath = "Assets/MainMenu/Fonts/Unbounded-ExtraBold SDF.asset";
        const string BodyFontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string RootName = "HUD";

        [MenuItem("Tools/Funseki/UI/Setup HUD in Slice_Day1")]
        public static void Build()
        {
            Directory.CreateDirectory(DataDir);
            var theme = EnsureTheme();
            var settings = EnsureSettings();

            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);

            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);
            var root = new GameObject(RootName);
            var view = root.AddComponent<HudView>();
            var so = new SerializedObject(view);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("[HudSetup] Slice_Day1: HUD placed under 'HUD' (theme Data/UI/HudTheme, settings Data/UI/HudSettings).");
        }

        static HudTheme EnsureTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);
            if (theme != null) return theme;
            theme = ScriptableObject.CreateInstance<HudTheme>();
            theme.titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            theme.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }

        static HudSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HudSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<HudSettings>();
            settings.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            settings.hints = new List<HudSettings.Hint>
            {
                Hint("move", HudHintTrigger.BreakTime, 0.5f, "Move", "Ходить", "WASD", "L-стик", false),
                Hint("interact", HudHintTrigger.InteractionTarget, 0.3f, "Interact", "Взаимодействовать", "", "X"),
                Hint("hero_menu", HudHintTrigger.BreakTime, 15f, "HeroMenu", "Сменить героя", "Tab", "Y"),
                Hint("ability", HudHintTrigger.BreakTime, 30f, "HeroAbility", "Особое действие героя", "Q", "B"),
                Hint("item_wheel", HudHintTrigger.ItemAdded, 1f, "CycleItem", "Сменить предмет в руке", "Колесо", "D-pad"),
                Hint("first_person", HudHintTrigger.BreakTime, 50f, "FirstPerson", "Смотреть от первого лица (держать)", "F", "LB"),
            };
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        static HudSettings.Hint Hint(string id, HudHintTrigger trigger, float delay, string action, string text, string key, string pad,
            bool skipIfUsed = true) =>
            new()
            {
                id = id, trigger = trigger, delay = delay, action = action, text = text,
                keyboardLabel = key, gamepadLabel = pad, skipIfAlreadyUsed = skipIfUsed,
            };
    }
}
