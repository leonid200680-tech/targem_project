using System;
using UnityEngine;
using UnityEngine.UI;

// Телевизор с камерами наблюдения. По E включается (сначала барахлит), дальше A/D листают
// камеры, E выключает. Интерфейс "монитора охраны" собирается из кода.
public class InteractableTV : MonoBehaviour
{
    public string prompt = "Нажмите E, чтобы включить телевизор";

    [Header("Камеры наблюдения")]
    public CCTVCamera[] cameras;
    public Vector2Int resolution = new Vector2Int(1280, 720);

    [Header("Оформление")]
    public Font font;
    public Color feedTint = new Color(0.8f, 0.92f, 0.82f);
    public AudioClip powerSound;

    [Header("Барахлит")]
    public float bootTime = 1.8f;            // сколько "не ловит" при включении
    public float switchNoiseTime = 0.35f;    // помехи при переключении камеры
    public Vector2 glitchInterval = new Vector2(3f, 8f);
    public float secondsToCountCamera = 1.2f;

    public bool IsViewing { get; private set; }
    public int CameraCount => cameras != null ? cameras.Length : 0;
    public int CamerasSeen { get; private set; }
    public bool AllCamerasSeen => CameraCount > 0 && CamerasSeen >= CameraCount;

    private bool[] seen;
    private int current;
    private float watchTime;
    private float noiseUntil;
    private float nextGlitch;
    private float jitterUntil;
    private int lastToggleFrame = -1;
    private PlayerController player;
    private RenderTexture feedTexture;

    private Canvas overlay;
    private RectTransform feedRect;
    private RawImage feed;
    private RawImage noise;
    private RawImage scanlines;
    private Text labelText;
    private Text timeText;
    private Text recText;
    private Text noSignalText;
    private AudioSource hiss;

    void Start()
    {
        seen = new bool[CameraCount];
        BuildOverlay();
        overlay.gameObject.SetActive(false);
    }

    public void Interact()
    {
        if (Time.frameCount == lastToggleFrame) return;
        lastToggleFrame = Time.frameCount;

        if (IsViewing) TurnOff();
        else TurnOn();
    }

    void TurnOn()
    {
        if (CameraCount == 0) return;

        IsViewing = true;
        player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.enabled = false;

        if (feedTexture == null)
        {
            feedTexture = new RenderTexture(resolution.x, resolution.y, 24) { name = "CCTV Feed" };
            feed.texture = feedTexture;
        }

        overlay.gameObject.SetActive(true);
        if (powerSound != null) AudioSource.PlayClipAtPoint(powerSound, transform.position);

        // Телек барахлит: сначала только помехи и "НЕТ СИГНАЛА"
        noiseUntil = Time.unscaledTime + bootTime;
        ScheduleGlitch();
        ShowCamera(current);
        hiss.Play();
    }

    void TurnOff()
    {
        IsViewing = false;
        foreach (var c in cameras) if (c != null) c.SetLive(false, null);
        overlay.gameObject.SetActive(false);
        hiss.Stop();
        if (player != null) player.enabled = true;
        if (powerSound != null) AudioSource.PlayClipAtPoint(powerSound, transform.position);
    }

    void ShowCamera(int index)
    {
        current = (index % CameraCount + CameraCount) % CameraCount;
        watchTime = 0f;
        for (int i = 0; i < CameraCount; i++)
        {
            if (cameras[i] != null) cameras[i].SetLive(i == current, feedTexture);
        }
        var cam = cameras[current];
        labelText.text = cam != null ? $"{cam.label}   {cam.location}" : "";
        noiseUntil = Mathf.Max(noiseUntil, Time.unscaledTime + switchNoiseTime);
    }

    void ScheduleGlitch() => nextGlitch = Time.unscaledTime + UnityEngine.Random.Range(glitchInterval.x, glitchInterval.y);

    void Update()
    {
        if (!IsViewing) return;

        // Пауза рисуется под монитором, поэтому на время паузы прячем его
        overlay.enabled = !PauseMenu.GameIsPaused;
        if (PauseMenu.GameIsPaused) return;

        if (Input.GetKeyDown(KeyCode.E)) { Interact(); return; }
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) ShowCamera(current + 1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) ShowCamera(current - 1);

        float t = Time.unscaledTime;

        // Случайные сбои: короткая потеря сигнала или дёргание картинки
        if (t >= nextGlitch)
        {
            if (UnityEngine.Random.value < 0.4f) noiseUntil = t + UnityEngine.Random.Range(0.15f, 0.5f);
            else jitterUntil = t + UnityEngine.Random.Range(0.1f, 0.3f);
            ScheduleGlitch();
        }

        bool noSignal = t < noiseUntil;
        if (!noSignal)
        {
            watchTime += Time.unscaledDeltaTime;
            cameras[current]?.OnWatched(watchTime);
            if (watchTime >= secondsToCountCamera && !seen[current])
            {
                seen[current] = true;
                CamerasSeen++;
            }
        }

        AnimateOverlay(t, noSignal);
    }

    void AnimateOverlay(float t, bool noSignal)
    {
        // Шум: каждый кадр сдвигаем текстуру, при потере сигнала он закрывает всё
        noise.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 3f, 2f);
        float noiseAlpha = noSignal ? 0.95f : 0.07f + 0.04f * Mathf.PerlinNoise(t * 3f, 0f);
        noise.color = new Color(1f, 1f, 1f, noiseAlpha);
        feed.color = noSignal ? new Color(feedTint.r, feedTint.g, feedTint.b, 0.25f) : feedTint;
        noSignalText.enabled = noSignal && Mathf.Repeat(t, 0.8f) < 0.55f;
        hiss.volume = noSignal ? 0.35f : 0.04f;

        // Дёргание по горизонтали и бегущие полосы развёртки
        float jitter = t < jitterUntil ? UnityEngine.Random.Range(-14f, 14f) : 0f;
        feedRect.anchoredPosition = new Vector2(jitter, 0f);
        scanlines.uvRect = new Rect(0f, -t * 0.6f, 1f, Screen.height / 3f);

        recText.enabled = Mathf.Repeat(t, 1.2f) < 0.8f;
        DateTime now = DateTime.Today.AddHours(3).AddMinutes(12).AddSeconds(Time.time);
        timeText.text = now.ToString("dd-MM-yyyy  HH:mm:ss");
    }

    void OnDisable()
    {
        if (IsViewing) TurnOff();
    }

    void OnDestroy()
    {
        if (feedTexture != null) feedTexture.Release();
        if (overlay != null) Destroy(overlay.gameObject);
    }

    // ---------- Интерфейс монитора ----------

    void BuildOverlay()
    {
        Font f = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("CCTV Monitor", typeof(Canvas), typeof(CanvasScaler));
        overlay = canvasGo.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = NewChild<Image>("Background", canvasGo.transform);
        Stretch(bg.rectTransform);
        bg.color = Color.black;

        feed = NewChild<RawImage>("Feed", canvasGo.transform);
        feedRect = feed.rectTransform;
        Stretch(feedRect);
        var fitter = feed.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = resolution.x / (float)resolution.y;

        noise = NewChild<RawImage>("Noise", feedRect);
        Stretch(noise.rectTransform);
        noise.texture = MakeNoiseTexture();

        scanlines = NewChild<RawImage>("Scanlines", feedRect);
        Stretch(scanlines.rectTransform);
        scanlines.texture = MakeScanlineTexture();
        scanlines.color = new Color(0f, 0f, 0f, 0.35f);

        var vignette = NewChild<RawImage>("Vignette", feedRect);
        Stretch(vignette.rectTransform);
        vignette.texture = MakeVignetteTexture();

        recText = NewText("REC", feedRect, f, 34, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(48, -36));
        recText.text = "● REC";
        recText.color = new Color(0.9f, 0.08f, 0.05f);

        labelText = NewText("Label", feedRect, f, 30, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(48, 40));
        timeText = NewText("Time", feedRect, f, 30, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-48, -36));

        noSignalText = NewText("NoSignal", feedRect, f, 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero);
        noSignalText.text = "НЕТ СИГНАЛА";

        var hint = NewText("Hint", feedRect, f, 24, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-48, 40));
        hint.text = "A / D — листать камеры      E — выключить";
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        hiss = canvasGo.AddComponent<AudioSource>();
        hiss.clip = MakeHissClip();
        hiss.loop = true;
        hiss.spatialBlend = 0f;
        hiss.playOnAwake = false;
        hiss.ignoreListenerPause = true;
    }

    static T NewChild<T>(string name, Transform parent) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        var g = go.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text NewText(string name, Transform parent, Font f, int size, TextAnchor align, Vector2 anchor, Vector2 pos)
    {
        var text = NewChild<Text>(name, parent);
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1100, 90);
        text.font = f;
        text.fontSize = size;
        text.alignment = align;
        text.color = new Color(0.92f, 0.95f, 0.9f, 0.9f);
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
        return text;
    }

    static Texture2D MakeNoiseTexture()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[size * size];
        var rnd = new System.Random(7);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rnd.Next(256);
            px[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeScanlineTexture()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        tex.SetPixels(new[] { Color.clear, Color.clear, Color.white, Color.white });
        tex.Apply();
        return tex;
    }

    static Texture2D MakeVignetteTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x / (size - 1f) * 2f - 1f, dy = y / (size - 1f) * 2f - 1f;
                float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.75f);
                px[y * size + x] = new Color(0f, 0f, 0f, a * a * 0.85f);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static AudioClip MakeHissClip()
    {
        const int rate = 22050;
        var data = new float[rate * 2];
        var rnd = new System.Random(3);
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.6f; // немного приглушаем верха
            data[i] = lp * 0.5f;
        }
        var clip = AudioClip.Create("CCTV Hiss", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
