using System;
using UnityEngine;
using UnityEngine.UI;

// Телевизор с камерами наблюдения. По E включается (сначала барахлит), дальше A/D листают
// камеры, E выключает. Интерфейс "монитора охраны" собирается из кода.
// Пугалки: в камере может мелькнуть бабка (в следующий раз - уже у самой камеры),
// а сквозь помехи иногда проступают надписи и часы сбиваются на 03:33.
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

    [Tooltip("Свет, который выключается, пока смотрят камеры (общий заполняющий свет пересвечивает ночной режим). " +
             "Если пусто - ищется объект \"Global Fill Light\"")]
    public Light[] hideWhileViewing;

    [Header("Пугалки на камерах")]
    [Tooltip("Кого показывать в камерах (если пусто - бабка из HallucinationManager)")]
    public GameObject apparitionSource;
    [Tooltip("Шанс, что в камере, куда смотрят впервые, кто-то появится")]
    [Range(0, 1)] public float apparitionChance = 0.5f;
    [Tooltip("Сколько раз за игру: в первый раз далеко, в следующий - уже у самой камеры")]
    public int maxApparitions = 2;
    public string[] creepyMessages = { "ОБЕРНИСЬ", "ОНА ЗДЕСЬ", "ТЫ НЕ ОДИН", "ПОМОГИ МНЕ", "ОНА ИДЁТ" };
    [Tooltip("Шанс, что при потере сигнала вместо \"НЕТ СИГНАЛА\" мелькнёт надпись")]
    [Range(0, 1)] public float creepyMessageChance = 0.25f;

    public bool IsViewing { get; private set; }
    public int CameraCount => cameras != null ? cameras.Length : 0;
    public int CamerasSeen { get; private set; }
    public bool AllCamerasSeen => CameraCount > 0 && CamerasSeen >= CameraCount;

    private bool[] seen;
    private int current;
    private float watchTime;
    private float switchLockUntil, exitLockUntil;   // чтобы пугалку нельзя было пролистать, не увидев
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
    private AudioSource sting;

    // Пугалки
    private GameObject apparition;
    private int apparitionsShown;
    private int[] hauntCount;          // сколько раз в камере уже кто-то был
    private float apparitionAfter = -1f;   // через сколько секунд просмотра появится (-1 - не запланировано)
    private float apparitionUntil;
    private string message;
    private float messageUntil;
    // Камера "врёт": в одной из камер стоит охранник (сам игрок) спиной к камере, хотя игрок сейчас у телевизора.
    // Один раз: переключился на другую камеру и обратно - его там уже нет.
    private GameObject doppel;
    private int doppelCamera = -1;
    private bool doppelDone;

    void Start()
    {
        seen = new bool[CameraCount];
        hauntCount = new int[CameraCount];
        apparition = MakeApparition();    // заранее, пока настоящая бабка ещё выключена
        HideApparition();
        BuildOverlay();
        overlay.gameObject.SetActive(false);
    }

    public void Interact()
    {
        if (IsViewing && Time.unscaledTime < exitLockUntil) return;   // пугалку нельзя пропустить выходом
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

        // Двойник - в камере 2 этажа (CAM 03): там он стоит в проёме кабинета, в свете ИК-подсветки
        if (doppelCamera < 0 && CameraCount > 1) doppelCamera = CameraCount > 2 ? 2 : (current + 1) % CameraCount;

        overlay.gameObject.SetActive(true);
        SetFillLights(false);
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
        HideApparition();
        if (doppel != null && doppel.activeSelf) { doppel.SetActive(false); doppelDone = true; }
        apparitionAfter = -1f;
        foreach (var c in cameras) if (c != null) c.SetLive(false, null);
        if (overlay != null) overlay.gameObject.SetActive(false);
        SetFillLights(true);
        if (hiss != null) hiss.Stop();
        if (player != null) player.enabled = true;
        // при выгрузке сцены (OnDisable) звук не создаём
        if (powerSound != null && isActiveAndEnabled) AudioSource.PlayClipAtPoint(powerSound, transform.position);
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

        // Пугалка: в новой камере - с шансом, в камере, где уже кто-то мелькал, - обязательно (и ближе)
        HideApparition();
        apparitionAfter = -1f;

        // Двойник: появляется в выбранной камере при первом показе, при уходе с неё исчезает навсегда
        if (doppel != null && doppel.activeSelf) { doppel.SetActive(false); doppelDone = true; }
        switchLockUntil = noiseUntil + 0.5f;
        if (!doppelDone && current == doppelCamera && ShowDoppel(cam))
        {
            switchLockUntil = exitLockUntil = noiseUntil + 2f;
            return;   // в этой камере больше ничего не нужно
        }
        if (apparitionsShown < maxApparitions && (hauntCount[current] > 0 || UnityEngine.Random.value < apparitionChance))
        {
            apparitionAfter = UnityEngine.Random.Range(0.3f, 1f);
            switchLockUntil = exitLockUntil = noiseUntil + apparitionAfter + 1.2f;
        }
    }

    void UpdateApparition(float t)
    {
        if (apparition != null && apparition.activeSelf)
        {
            if (t < apparitionUntil) return;
            // Исчезает вместе с помехой
            HideApparition();
            noiseUntil = t + UnityEngine.Random.Range(0.3f, 0.6f);
            jitterUntil = noiseUntil;
            return;
        }

        if (apparitionAfter < 0f || watchTime < apparitionAfter) return;
        apparitionAfter = -1f;

        float dist = hauntCount[current] == 0 ? UnityEngine.Random.Range(7f, 10f) : 3f;
        if (!PlaceApparition(cameras[current], dist)) return;
        apparition.SetActive(true);
        hauntCount[current]++;
        apparitionsShown++;
        apparitionUntil = t + (hauntCount[current] > 1 ? UnityEngine.Random.Range(0.6f, 1f) : UnityEngine.Random.Range(1.2f, 2f));
        jitterUntil = t + 0.15f;
        if (sting != null) sting.Play();
    }

    // Ставит фигуру перед камерой: на пол, в кадре и чтобы ничего не загораживало
    bool PlaceApparition(CCTVCamera cam, float dist, float minDist = 2.5f, float step = 1f)
    {
        if (cam == null) return false;
        if (apparition == null) apparition = MakeApparition();
        if (apparition == null) return false;

        Transform t = cam.transform;
        Vector3 fwd = t.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return false;
        fwd.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, fwd);

        for (float d = dist; d >= minDist; d -= step)
        {
            Vector3 p = t.position + fwd * d + side * UnityEngine.Random.Range(-0.6f, 0.6f);
            if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.point.y > t.position.y - 1.5f) continue;
            Vector3 chest = hit.point + Vector3.up * 1.2f;
            if (Physics.Linecast(t.position, chest, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 vp = cam.Cam.WorldToViewportPoint(chest);
            if (vp.z <= 0f || vp.x < 0.15f || vp.x > 0.85f || vp.y < 0.1f || vp.y > 0.9f) continue;

            Vector3 look = t.position - hit.point; look.y = 0f;
            apparition.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(look));
            return true;
        }
        return false;
    }

    // Копия бабки без поведения: только модель и анимация
    GameObject MakeApparition()
    {
        var src = apparitionSource;
        if (src == null)
        {
            var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
            if (hm != null) src = hm.realGhost;
        }
        if (src == null) return null;

        bool wasActive = src.activeSelf;
        src.SetActive(false);              // копия создаётся выключенной, её скрипты не успеют запуститься
        var go = Instantiate(src);
        src.SetActive(wasActive);
        go.name = "CCTV Apparition";
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(b);
        foreach (var a in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var s in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(s);
        return go;
    }

    // Охранник стоит спиной к камере и смотрит вглубь коридора. Без звука и помех - просто стоит.
    bool ShowDoppel(CCTVCamera cam)
    {
        if (doppel == null) doppel = MakeDoppel();
        if (doppel == null || cam == null) return false;

        // Место ищем так же, как для бабки (временно ставим туда копию бабки и берём её позицию)
        var keep = apparition;
        apparition = doppel;
        bool placed = PlaceApparition(cam, 3.6f, 1.4f, 0.3f);
        apparition = keep;
        if (!placed) return false;

        doppel.transform.rotation = Quaternion.LookRotation(-(doppel.transform.rotation * Vector3.forward));   // спиной к камере
        doppel.SetActive(true);
        return true;
    }

    // Копия модели игрока без поведения, целиком (с головой), в спокойной стойке
    GameObject MakeDoppel()
    {
        var body = FindAnyObjectByType<PlayerBodyAnimator>(FindObjectsInactive.Include);
        if (body == null) return null;
        var holder = new GameObject("tmp");
        holder.SetActive(false);
        var go = Instantiate(body.gameObject, holder.transform);
        go.name = "CCTV Doppelganger";
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(b);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        var hands = go.transform.Find("FPS_HANDS");
        if (hands != null) DestroyImmediate(hands.gameObject);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        go.transform.SetParent(null, false);
        go.transform.localScale = body.transform.lossyScale;
        go.SetActive(false);
        Destroy(holder);
        return go;
    }

    void HideApparition()
    {
        if (apparition != null) apparition.SetActive(false);
    }

    void SetFillLights(bool on)
    {
        if (hideWhileViewing == null || hideWhileViewing.Length == 0)
        {
            var go = GameObject.Find("Global Fill Light");
            hideWhileViewing = go != null ? go.GetComponents<Light>() : new Light[0];
        }
        foreach (var l in hideWhileViewing) if (l != null) l.enabled = on;
    }

    public bool TryExit()
    {
        if (!IsViewing) return false;
        Interact();
        return !IsViewing;
    }

    void ScheduleGlitch() => nextGlitch = Time.unscaledTime + UnityEngine.Random.Range(glitchInterval.x, glitchInterval.y);

    void Update()
    {
        if (!IsViewing) return;

        // Пауза рисуется под монитором, поэтому на время паузы прячем его
        overlay.enabled = !PauseMenu.GameIsPaused;
        if (PauseMenu.GameIsPaused) return;

        if (BotInput.GetKeyDown(KeyCode.E) && TryExit()) return;
        if (Time.unscaledTime >= switchLockUntil)
        {
            if (BotInput.GetKeyDown(KeyCode.D) || BotInput.GetKeyDown(KeyCode.RightArrow)) ShowCamera(current + 1);
            if (BotInput.GetKeyDown(KeyCode.A) || BotInput.GetKeyDown(KeyCode.LeftArrow)) ShowCamera(current - 1);
        }

        float t = Time.unscaledTime;

        // Случайные сбои: короткая потеря сигнала или дёргание картинки
        if (t >= nextGlitch)
        {
            if (UnityEngine.Random.value < 0.4f)
            {
                noiseUntil = t + UnityEngine.Random.Range(0.15f, 0.5f);
                // Иногда сквозь помехи проступает надпись
                if (CamerasSeen > 0 && creepyMessages.Length > 0 && UnityEngine.Random.value < creepyMessageChance)
                {
                    message = creepyMessages[UnityEngine.Random.Range(0, creepyMessages.Length)];
                    noiseUntil = t + UnityEngine.Random.Range(0.6f, 1f);
                    messageUntil = noiseUntil;
                }
            }
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
        UpdateApparition(t);

        AnimateOverlay(t, noSignal);
    }

    void AnimateOverlay(float t, bool noSignal)
    {
        // Шум: каждый кадр сдвигаем текстуру, при потере сигнала он закрывает всё
        noise.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 3f, 2f);
        float noiseAlpha = noSignal ? 0.95f : 0.07f + 0.04f * Mathf.PerlinNoise(t * 3f, 0f);
        noise.color = new Color(1f, 1f, 1f, noiseAlpha);
        feed.color = noSignal ? new Color(feedTint.r, feedTint.g, feedTint.b, 0.25f) : feedTint;
        bool showMessage = noSignal && t < messageUntil;
        noSignalText.text = showMessage ? message : "НЕТ СИГНАЛА";
        noSignalText.color = showMessage ? new Color(0.85f, 0.05f, 0.04f, 0.95f) : new Color(0.92f, 0.95f, 0.9f, 0.9f);
        noSignalText.enabled = showMessage || (noSignal && Mathf.Repeat(t, 0.8f) < 0.55f);
        hiss.volume = noSignal ? 0.5f : 0.05f;

        // Дёргание по горизонтали и бегущие полосы развёртки
        float jitter = t < jitterUntil ? UnityEngine.Random.Range(-14f, 14f) : 0f;
        feedRect.anchoredPosition = new Vector2(jitter, 0f);
        scanlines.uvRect = new Rect(0f, -t * 0.6f, 1f, Screen.height / 3f);

        recText.enabled = Mathf.Repeat(t, 1.2f) < 0.8f;
        DateTime now = DateTime.Today.AddHours(3).AddMinutes(12).AddSeconds(Time.time);
        // Пока мелькает надпись, часы сбиваются на 03:33:33
        timeText.text = t < messageUntil ? now.ToString("dd-MM-yyyy") + "  03:33:33" : now.ToString("dd-MM-yyyy  HH:mm:ss");
    }

    void OnDisable()
    {
        if (IsViewing) TurnOff();
    }

    void OnDestroy()
    {
        if (feedTexture != null) feedTexture.Release();
        if (apparition != null) Destroy(apparition);
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
        hint.text = "A / D — листать камеры      E / Esc — выключить";
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        hiss = canvasGo.AddComponent<AudioSource>();
        hiss.clip = MakeHissClip();
        hiss.loop = true;
        hiss.spatialBlend = 0f;
        hiss.playOnAwake = false;
        hiss.ignoreListenerPause = true;

        sting = canvasGo.AddComponent<AudioSource>();
        sting.clip = MakeStingClip();
        sting.spatialBlend = 0f;
        sting.playOnAwake = false;
        sting.volume = 1f;
    }

    // Глухой удар с нарастающим скрежетом помех - когда в камере кто-то появляется
    static AudioClip MakeStingClip()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.6f);
        var data = new float[n];
        var rnd = new System.Random(11);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 2.2f) * Mathf.Clamp01(t * 60f);
            phase += 2f * Mathf.PI * Mathf.Lerp(62f, 38f, Mathf.Clamp01(t / 1.2f)) / rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.35f;
            float scrape = lp * Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 70f));
            data[i] = Mathf.Clamp(boom * 0.8f + scrape * 0.35f, -1f, 1f);
        }
        var clip = AudioClip.Create("CCTV Sting", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
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
