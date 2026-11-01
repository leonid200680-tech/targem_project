using UnityEngine;
using UnityEngine.Audio;

// Громкость игры.
// Общая громкость - через AudioListener.volume: действует на любой звук, даже не подключённый к микшеру.
// Музыка и звуки - через группы микшера. Все источники без группы (в том числе созданные из кода
// и PlayClipAtPoint) автоматически отправляются в группу "SFX", музыка - в "Music".
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    [Tooltip("Источники, в имени объекта или клипа которых есть эти слова, считаются музыкой")]
    [SerializeField] private string[] musicKeywords = { "Music", "Музык" };
    [SerializeField] private float routeInterval = 0.1f;

    private AudioMixerGroup musicGroup;
    private AudioMixerGroup sfxGroup;
    private float nextRoute;
    private int reapplyFrames = 3;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Этот объект переживёт смену сцен

        if (mainMixer != null)
        {
            musicGroup = FindGroup("Music");
            sfxGroup = FindGroup("SFX");
        }
    }

    private void Start()
    {
        LoadVolumeSettings();
        RouteSources();
    }

    private void Update()
    {
        // В билде микшер может проигнорировать SetFloat в первые кадры - повторяем
        if (reapplyFrames > 0 && --reapplyFrames == 0) LoadVolumeSettings();

        if (Time.unscaledTime >= nextRoute)
        {
            nextRoute = Time.unscaledTime + routeInterval;
            RouteSources();
        }
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(GameSettings.MasterKey, value);
    }

    public void SetMusicVolume(float value) => SetVolume("MusicVol", GameSettings.MusicKey, value);
    public void SetSFXVolume(float value) => SetVolume("SFXVol", GameSettings.SFXKey, value);

    private void SetVolume(string mixerParam, string prefKey, float value)
    {
        // Логарифмическая шкала для плавной регулировки
        float clampedValue = Mathf.Clamp(value, 0.0001f, 1f);
        float dB = Mathf.Log10(clampedValue) * 20f;

        if (mainMixer != null)
            mainMixer.SetFloat(mixerParam, dB);

        PlayerPrefs.SetFloat(prefKey, value);
    }

    private void LoadVolumeSettings()
    {
        // Общий уровень микшера держим на 0 дБ: общая громкость теперь у AudioListener
        if (mainMixer != null) mainMixer.SetFloat("MasterVol", 0f);
        SetMasterVolume(PlayerPrefs.GetFloat(GameSettings.MasterKey, GameSettings.DefaultMaster));
        SetMusicVolume(PlayerPrefs.GetFloat(GameSettings.MusicKey, GameSettings.DefaultMusic));
        SetSFXVolume(PlayerPrefs.GetFloat(GameSettings.SFXKey, GameSettings.DefaultSFX));
    }

    // Отправляет все источники без группы в микшер
    private void RouteSources()
    {
        if (sfxGroup == null) return;
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (source.outputAudioMixerGroup != null) continue;
            source.outputAudioMixerGroup = IsMusic(source) && musicGroup != null ? musicGroup : sfxGroup;
        }
    }

    private bool IsMusic(AudioSource source)
    {
        string objName = source.gameObject.name;
        string clipName = source.clip != null ? source.clip.name : "";
        foreach (var word in musicKeywords)
            if (objName.Contains(word) || clipName.Contains(word)) return true;
        return false;
    }

    private AudioMixerGroup FindGroup(string name)
    {
        foreach (var g in mainMixer.FindMatchingGroups(""))
            if (g.name == name) return g;
        return null;
    }
}
