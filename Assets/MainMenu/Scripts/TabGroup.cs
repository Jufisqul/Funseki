using UnityEngine;

// Switches between pages of a menu panel; tab buttons call Show(index).
public class TabGroup : MonoBehaviour
{
    public GameObject[] pages;
    public MenuTab[] tabs;

    void Awake() => Show(0);

    public void Show(int index)
    {
        for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == index);
        for (int i = 0; i < tabs.Length; i++) tabs[i].SetOn(i == index);
    }

    public void ShowWithSound(int index)
    {
        MenuAudio.Click();
        Show(index);
    }
}
