using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tab header in a menu panel: filled when active.
public class MenuTab : MonoBehaviour
{
    public Image background;
    public TMP_Text label;
    public Color onBg = new Color(1f, 0.36f, 0.48f), offBg = new Color(1f, 1f, 1f, 0f);
    public Color onText = Color.white, offText = new Color(0.11f, 0.11f, 0.13f);

    public void SetOn(bool on)
    {
        background.color = on ? onBg : offBg;
        label.color = on ? onText : offText;
    }
}
