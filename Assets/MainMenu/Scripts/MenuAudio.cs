using UnityEngine;

// Menu music and UI sounds; volumes follow GameSettings.
public class MenuAudio : MonoBehaviour
{
    public static MenuAudio Instance { get; private set; }

    public AudioSource music;
    public AudioSource sfx;
    public AudioClip hover, click, open, back;
    [Range(0, 1)] public float musicBaseVolume = 0.7f;

    void Awake()
    {
        Instance = this;
        GameSettings.Changed += ApplyVolumes;
        ApplyVolumes();
    }

    void OnDestroy() => GameSettings.Changed -= ApplyVolumes;

    void ApplyVolumes()
    {
        if (music) music.volume = musicBaseVolume * GameSettings.Music;
        if (sfx) sfx.volume = GameSettings.Sfx;
    }

    public static void Hover() => Play(Instance ? Instance.hover : null, 0.5f);
    public static void Click() => Play(Instance ? Instance.click : null, 1f);
    public static void Open() => Play(Instance ? Instance.open : null, 0.8f);
    public static void Back() => Play(Instance ? Instance.back : null, 0.8f);

    static void Play(AudioClip clip, float volume)
    {
        if (clip && Instance && Instance.sfx) Instance.sfx.PlayOneShot(clip, volume);
    }
}
