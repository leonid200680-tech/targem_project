using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    private const string MasterKey = "MasterVolume";
    private const string MusicKey = "MusicVolume";
    private const string SFXKey = "SFXVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Этот объект переживёт смену сцен
    }

    private void Start()
    {
        // Загружаем сохранённые настройки при старте
        LoadVolumeSettings();
    }

    public void SetMasterVolume(float value) => SetVolume("MasterVol", MasterKey, value);
    public void SetMusicVolume(float value) => SetVolume("MusicVol", MusicKey, value);
    public void SetSFXVolume(float value) => SetVolume("SFXVol", SFXKey, value);

    private void SetVolume(string mixerParam, string prefKey, float value)
    {
        // Ключевой момент: логарифмическая шкала для плавной регулировки
        float clampedValue = Mathf.Clamp(value, 0.0001f, 1f);
        float dB = Mathf.Log10(clampedValue) * 20f;

        if (mainMixer != null)
            mainMixer.SetFloat(mixerParam, dB);

        PlayerPrefs.SetFloat(prefKey, value);
        PlayerPrefs.Save(); // Сохраняем изменения сразу
    }

    private void LoadVolumeSettings()
    {
        float master = PlayerPrefs.GetFloat(MasterKey, 1f);
        float music = PlayerPrefs.GetFloat(MusicKey, 1f);
        float sfx = PlayerPrefs.GetFloat(SFXKey, 1f);

        SetMasterVolume(master);
        SetMusicVolume(music);
        SetSFXVolume(sfx);
    }
}