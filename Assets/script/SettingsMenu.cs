using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Панель настроек (главное меню и пауза): звук, управление, графика.
// Значения хранятся в PlayerPrefs через GameSettings / AudioManager.
public class SettingsMenu : MonoBehaviour
{
    [Header("Звук")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text sfxValue;

    [Header("Управление")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValue;

    [Header("Графика")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TMP_Text brightnessValue;
    [SerializeField] private Dropdown qualityDropdown;
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown displayModeDropdown;
    [SerializeField] private Dropdown vsyncDropdown;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private float saveAt = -1f;

    private void Start()
    {
        SetupAudio();
        SetupControls();
        SetupGraphics();
    }

    private void Update()
    {
        // Сохраняем на диск не на каждое движение ползунка, а чуть позже
        if (saveAt > 0f && Time.unscaledTime >= saveAt)
        {
            saveAt = -1f;
            GameSettings.Save();
        }
    }

    private void MarkDirty() => saveAt = Time.unscaledTime + 0.5f;

    // ---------- Звук ----------

    private void SetupAudio()
    {
        BindSlider(masterSlider, masterValue, PlayerPrefs.GetFloat(GameSettings.MasterKey, GameSettings.DefaultMaster), Percent, v =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v);
            else AudioListener.volume = v;
        });
        BindSlider(musicSlider, musicValue, PlayerPrefs.GetFloat(GameSettings.MusicKey, GameSettings.DefaultMusic), Percent, v =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v);
        });
        BindSlider(sfxSlider, sfxValue, PlayerPrefs.GetFloat(GameSettings.SFXKey, GameSettings.DefaultSFX), Percent, v =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v);
        });
    }

    // ---------- Управление ----------

    private void SetupControls()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.2f;
            sensitivitySlider.maxValue = 6f;
        }
        BindSlider(sensitivitySlider, sensitivityValue, GameSettings.MouseSensitivity, v => v.ToString("0.0"), GameSettings.SetMouseSensitivity);
    }

    // ---------- Графика ----------

    private void SetupGraphics()
    {
        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = -2f;
            brightnessSlider.maxValue = 2f;
        }
        BindSlider(brightnessSlider, brightnessValue, GameSettings.Brightness, v => (v > 0.04f ? "+" : "") + v.ToString("0.0"), GameSettings.SetBrightness);

        if (qualityDropdown != null)
        {
            var names = new List<string>();
            foreach (var n in QualitySettings.names) names.Add(QualityName(n));
            BindDropdown(qualityDropdown, names, GameSettings.Quality, GameSettings.SetQuality);
        }

        if (displayModeDropdown != null)
            BindDropdown(displayModeDropdown, new List<string> { "Полный экран", "В окне" }, GameSettings.Fullscreen ? 0 : 1,
                i => GameSettings.SetFullscreen(i == 0));

        if (vsyncDropdown != null)
            BindDropdown(vsyncDropdown, new List<string> { "Вкл", "Выкл" }, GameSettings.VSync ? 0 : 1,
                i => GameSettings.SetVSync(i == 0));

        if (resolutionDropdown != null) SetupResolutions();
    }

    private void SetupResolutions()
    {
        // Без повторов: одно разрешение - одна строка (частоты не важны)
        resolutions.Clear();
        var options = new List<string>();
        int current = 0;
        foreach (var r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (resolutions.Contains(size)) continue;
            resolutions.Add(size);
            options.Add(r.width + " x " + r.height);
        }
        for (int i = 0; i < resolutions.Count; i++)
            if (resolutions[i].x == Screen.width && resolutions[i].y == Screen.height) current = i;

        int saved = PlayerPrefs.GetInt("ResolutionIndex", current);
        if (saved < 0 || saved >= resolutions.Count) saved = current;
        BindDropdown(resolutionDropdown, options, saved, SetResolution);
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        Screen.SetResolution(resolutions[index].x, resolutions[index].y, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    // ---------- Вспомогательное ----------

    private void BindSlider(Slider slider, TMP_Text label, float value, System.Func<float, string> format, System.Action<float> apply)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        if (label != null) label.text = format(value);
        slider.onValueChanged.AddListener(v =>
        {
            apply(v);
            if (label != null) label.text = format(v);
            MarkDirty();
        });
    }

    private void BindDropdown(Dropdown dropdown, List<string> options, int value, System.Action<int> apply)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Count - 1));
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(i =>
        {
            apply(i);
            MarkDirty();
        });
    }

    private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    private static string QualityName(string unityName)
    {
        switch (unityName)
        {
            case "High Fidelity": return "Высокое";
            case "Balanced": return "Среднее";
            case "Performant": return "Низкое";
        }
        return unityName;
    }
}
