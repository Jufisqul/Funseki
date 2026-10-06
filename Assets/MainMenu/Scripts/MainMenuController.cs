using System.Collections;
using Funseki.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title menu: the camera stands in the middle of the schoolyard and turns to each section
// (GDD: front = school entrance, left = toilet & bikes, right = blank wall, back = gate).
public class MainMenuController : MonoBehaviour
{
    public enum Section { Home, Collection, Settings, Exit, Continue }

    [System.Serializable]
    public class View
    {
        public Section section;
        public Transform pose;
        public CanvasGroup panel;
        public Selectable firstSelected;
        public float fov = 45f;
    }

    public Camera cam;
    public View[] views;
    public CanvasGroup fade;
    public GameObject continueButton;
    public string introScene = "Intro";
    public string gameScene = "Game";
    public float turnSpeed = 2.6f;
    public float idleSway = 0.25f;

    Section current = Section.Home;
    bool busy;

    void Start()
    {
        SetContinueAvailable(SaveSystem != null && SaveSystem.HasSave);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var home = Find(Section.Home);
        cam.transform.SetPositionAndRotation(home.pose.position, home.pose.rotation);
        cam.fieldOfView = home.fov;
        foreach (var v in views)
            if (v.panel) SetPanel(v.panel, v.section == Section.Home ? 1f : 0f);
        fade.alpha = 1f;
        StartCoroutine(Fade(0f, 1.6f));
        Select(home);
    }

    void Update()
    {
        var v = Find(current);
        float k = 1f - Mathf.Exp(-turnSpeed * Time.unscaledDeltaTime);
        float t = Time.unscaledTime;
        var sway = Quaternion.Euler(Mathf.Sin(t * 0.21f) * idleSway, Mathf.Sin(t * 0.13f) * idleSway * 1.6f, 0f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, v.pose.position, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, v.pose.rotation * sway, k);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, v.fov, k);

        foreach (var view in views)
        {
            if (!view.panel) continue;
            float target = view.section == current && !busy ? 1f : 0f;
            SetPanel(view.panel, Mathf.MoveTowards(view.panel.alpha, target, Time.unscaledDeltaTime * 3.5f));
        }

        bool backPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (backPressed && !busy && current != Section.Home) Back();
    }

    // ---- button hooks (wired in the scene)
    public void NewGame()
    {
        if (busy) return;
        SaveSystem?.ResetForNewGame();
        StartCoroutine(LoadScene(introScene, 0.2f));
    }

    // Back to the last autosave: flags, inventory and journal now, the day and the heroes when the scene loads.
    public void Continue()
    {
        if (busy || SaveSystem == null || !SaveSystem.PrepareContinue(out var scene)) return;
        Go(Section.Continue);
        StartCoroutine(LoadScene(string.IsNullOrEmpty(scene) ? gameScene : scene, 1.1f));
    }

    static ISaveSystem SaveSystem => ServiceLocator.TryGet<ISaveSystem>(out var s) ? s : null;

    // «Продолжить» stays in the menu but greyed out and unclickable while there is no save.
    void SetContinueAvailable(bool available)
    {
        continueButton.SetActive(true);
        if (!continueButton.TryGetComponent<CanvasGroup>(out var group)) group = continueButton.AddComponent<CanvasGroup>();
        group.alpha = available ? 1f : 0.4f;
        group.interactable = available;
        group.blocksRaycasts = available;
        foreach (var fx in continueButton.GetComponentsInChildren<MenuButtonFx>(true)) fx.enabled = available;
    }

    public void OpenCollection() => Go(Section.Collection);
    public void OpenSettings() => Go(Section.Settings);
    public void OpenExit() => Go(Section.Exit);

    public void Back()
    {
        MenuAudio.Back();
        Go(Section.Home);
    }

    public void QuitGame()
    {
        if (busy) return;
        StartCoroutine(QuitRoutine());
    }

    // ---- internals
    void Go(Section s)
    {
        if (busy || s == current) return;
        if (s != Section.Home) MenuAudio.Open();
        current = s;
        Select(Find(s));
    }

    void Select(View v)
    {
        if (EventSystem.current && v.firstSelected) EventSystem.current.SetSelectedGameObject(v.firstSelected.gameObject);
    }

    View Find(Section s)
    {
        foreach (var v in views) if (v.section == s) return v;
        return views[0];
    }

    static void SetPanel(CanvasGroup g, float a)
    {
        g.alpha = a;
        bool on = a > 0.95f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    IEnumerator Fade(float to, float duration)
    {
        float from = fade.alpha;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fade.alpha = to;
        fade.blocksRaycasts = to > 0.5f;
    }

    IEnumerator LoadScene(string scene, float delay)
    {
        busy = true;
        yield return new WaitForSecondsRealtime(delay);
        yield return Fade(1f, 1.0f);
        if (Application.CanStreamedLevelBeLoaded(scene))
        {
            SceneManager.LoadScene(scene);
            yield break;
        }
        Debug.LogWarning($"[MainMenu] Scene '{scene}' is not in Build Settings yet — returning to the menu.");
        yield return new WaitForSecondsRealtime(0.4f);
        busy = false;
        current = Section.Home;
        Select(Find(Section.Home));
        yield return Fade(0f, 1.0f);
    }

    IEnumerator QuitRoutine()
    {
        busy = true;
        yield return Fade(1f, 0.8f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
