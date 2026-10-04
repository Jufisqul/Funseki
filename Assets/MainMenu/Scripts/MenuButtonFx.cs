using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Skewed menu plate: slides right and recolours on hover or keyboard selection.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    public RectTransform plate;
    public Image background;
    public TMP_Text label;
    public TMP_Text hint;
    public Color normalBg = Color.white, hotBg = new Color(1f, 0.36f, 0.48f);
    public Color normalText = new Color(0.11f, 0.11f, 0.13f), hotText = Color.white;
    public float shift = 22f;

    bool hovered, selected;
    float t;
    Vector2 basePos;

    void Awake() => basePos = plate.anchoredPosition;

    void OnDisable() { hovered = selected = false; t = 0; Apply(); }

    void Update()
    {
        float target = hovered || selected ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 7f);
        Apply();
    }

    void Apply()
    {
        float e = 1f - (1f - t) * (1f - t);
        plate.anchoredPosition = basePos + new Vector2(shift * e, 0);
        background.color = Color.Lerp(normalBg, hotBg, e);
        label.color = Color.Lerp(normalText, hotText, e);
        if (hint) hint.color = Color.Lerp(normalText, hotText, e) * new Color(1, 1, 1, 0.6f);
    }

    public void OnPointerEnter(PointerEventData _) { hovered = true; MenuAudio.Hover(); }
    public void OnPointerExit(PointerEventData _) => hovered = false;
    public void OnSelect(BaseEventData _) { if (!selected && !hovered) MenuAudio.Hover(); selected = true; }
    public void OnDeselect(BaseEventData _) => selected = false;
    public void OnSubmit(BaseEventData _) => MenuAudio.Click();
    public void OnPointerClick(PointerEventData _) => MenuAudio.Click();
}
