using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Resolution")]
    [SerializeField] private Dropdown resolutionDropdown;
    private Resolution[] resolutions;

    private void Start()
    {
        // ==========================================
        // 1. НАСТРОЙКА ЗВУКА
        // ==========================================

        // Загружаем сохранённые значения (по умолчанию 0.75, чтобы не орало на весь дом)
        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.75f);

        // Проверяем, существует ли AudioManager (защита от вылета, если его нет на сцене)
        if (AudioManager.Instance != null)
        {
            // Сначала применяем загруженные настройки к самому аудиоменеджеру
            AudioManager.Instance.SetMasterVolume(masterSlider.value);
            AudioManager.Instance.SetMusicVolume(musicSlider.value);
            AudioManager.Instance.SetSFXVolume(sfxSlider.value);

            // Подписываемся на изменения ползунков
            masterSlider.onValueChanged.AddListener(AudioManager.Instance.SetMasterVolume);
            musicSlider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume);
        }
        else
        {
            Debug.LogWarning("AudioManager не найден на сцене! Ползунки звука не будут работать.");
        }

        // ==========================================
        // 2. НАСТРОЙКА РАЗРЕШЕНИЯ ЭКРАНА
        // ==========================================

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        int currentResolutionIndex = 0;
        var options = new System.Collections.Generic.List<string>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height + " " + resolutions[i].refreshRate + "Hz";
            options.Add(option);

            // Сравниваем с текущим разрешением ОКНА (Screen.width), а не рабочего стола
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
            {
                currentResolutionIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);

        // ЗАЩИТА ОТ ОШИБКИ: проверяем сохранённый индекс
        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", currentResolutionIndex);

        // Если сохранённый индекс больше, чем количество доступных разрешений — сбрасываем на текущее
        if (savedIndex >= resolutions.Length)
        {
            savedIndex = currentResolutionIndex;
        }

        resolutionDropdown.value = savedIndex;
        resolutionDropdown.RefreshShownValue();

        // Подписываемся на изменение выбора разрешения
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    public void SetResolution(int resolutionIndex)
    {
        // Дополнительная защита от выхода за пределы массива
        if (resolutionIndex < 0 || resolutionIndex >= resolutions.Length) return;

        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        // Сохраняем выбор
        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.Save();
    }
}