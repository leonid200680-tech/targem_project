using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Все настройки игрока в одном месте: хранятся в PlayerPrefs и применяются при запуске.
// Звук применяет AudioManager, остальное (мышь, яркость, графика) - здесь.
public static class GameSettings
{
    public const string MasterKey = "MasterVolume";
    public const string MusicKey = "MusicVolume";
    public const string SFXKey = "SFXVolume";
    const string SensitivityKey = "MouseSensitivity";
    const string BrightnessKey = "Brightness";
    const string QualityKey = "QualityLevel";
    const string FullscreenKey = "FullscreenMode";
    const string VSyncKey = "VSync";

    public const float DefaultMaster = 0.8f;
    public const float DefaultMusic = 0.6f;
    public const float DefaultSFX = 0.8f;
    public const float DefaultSensitivity = 2f;

    // ---------- Мышь ----------
    public static float MouseSensitivity { get; private set; } = DefaultSensitivity;

    public static void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0.2f, 6f);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
    }

    // ---------- Яркость: компенсация экспозиции HDRP, -2..+2 EV ----------
    public static float Brightness { get; private set; }
    static Volume brightnessVolume;
    static Exposure brightnessExposure;

    public static void SetBrightness(float ev)
    {
        Brightness = Mathf.Clamp(ev, -2f, 2f);
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
        EnsureBrightnessVolume();
        brightnessExposure.compensation.value = Brightness;

        // В темноте автоэкспозиция упирается в пределы профиля сцены, и одна компенсация
        // не может сделать картинку светлее. Поэтому сдвигаем и пределы на ту же величину.
        if (FindSceneExposureLimits(out float min, out float max))
        {
            brightnessExposure.limitMin.overrideState = true;
            brightnessExposure.limitMax.overrideState = true;
            brightnessExposure.limitMin.value = min - Brightness;
            brightnessExposure.limitMax.value = max - Brightness;
        }
    }

    // Пределы автоэкспозиции из самого приоритетного Volume, который видит камера игрока
    // (ночной режим камер наблюдения на своём слое сюда не попадает)
    static bool FindSceneExposureLimits(out float min, out float max)
    {
        min = max = 0f;
        float bestPriority = float.MinValue;
        bool found = false;
        int mask = 1; // слой Default, как у камеры игрока по умолчанию
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out HDAdditionalCameraData hd)) mask = hd.volumeLayerMask;

        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
        {
            if (v == brightnessVolume || !v.isGlobal || v.sharedProfile == null) continue;
            if ((mask & (1 << v.gameObject.layer)) == 0) continue;
            if (!v.sharedProfile.TryGet<Exposure>(out var e) || !e.limitMin.overrideState) continue;
            if (e.mode.overrideState && e.mode.value == ExposureMode.Fixed) continue;
            if (v.priority < bestPriority) continue;
            bestPriority = v.priority;
            min = e.limitMin.value;
            max = e.limitMax.value;
            found = true;
        }
        return found;
    }

    // Отдельный глобальный Volume поверх атмосферы сцены. Меняет только компенсацию
    // (и сдвигает пределы), режим автоэкспозиции остаётся из профиля сцены.
    static void EnsureBrightnessVolume()
    {
        if (brightnessVolume != null) return;
        var go = new GameObject("Brightness (GameSettings)");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.DontSave;
        brightnessVolume = go.AddComponent<Volume>();
        brightnessVolume.isGlobal = true;
        brightnessVolume.priority = 1000f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        brightnessExposure = profile.Add<Exposure>();
        brightnessExposure.compensation.overrideState = true;
        brightnessVolume.sharedProfile = profile;
    }

    // ---------- Графика ----------
    public static int Quality { get; private set; }

    public static void SetQuality(int level)
    {
        Quality = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(Quality, true);
        PlayerPrefs.SetInt(QualityKey, Quality);
        // Смена уровня качества сбрасывает vSync - возвращаем выбор игрока
        ApplyVSync();
    }

    public static bool Fullscreen { get; private set; } = true;

    public static void SetFullscreen(bool fullscreen)
    {
        Fullscreen = fullscreen;
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
    }

    public static bool VSync { get; private set; } = true;

    public static void SetVSync(bool on)
    {
        VSync = on;
        PlayerPrefs.SetInt(VSyncKey, on ? 1 : 0);
        ApplyVSync();
    }

    static void ApplyVSync() => QualitySettings.vSyncCount = VSync ? 1 : 0;

    // Для нового игрока - "Среднее" (Balanced), если такой уровень есть
    static int DefaultQualityLevel()
    {
        int balanced = System.Array.IndexOf(QualitySettings.names, "Balanced");
        return balanced >= 0 ? balanced : QualitySettings.GetQualityLevel();
    }

    // ---------- Загрузка при старте игры ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LoadAll()
    {
        MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        VSync = PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        SetQuality(PlayerPrefs.GetInt(QualityKey, DefaultQualityLevel()));
        SetBrightness(PlayerPrefs.GetFloat(BrightnessKey, 0f));
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        if (!Application.isEditor) SetFullscreen(Fullscreen);

        // После перезагрузки сцены (выход в меню) пересчитываем яркость под новую сцену
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => SetBrightness(Brightness);
    }

    public static void Save() => PlayerPrefs.Save();
}
