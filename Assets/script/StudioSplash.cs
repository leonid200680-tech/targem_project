using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Вступление при запуске игры:
// 1) экран "18+ / отказ от ответственности" (RU + EN), ждёт любую клавишу;
// 2) логотип "ON THE KNEE / STUDIO" (шрифт печатной машинки) с мерцанием, глитчами и синтезаторным ударом;
// 3) из темноты проявляется главное меню.
// Показывается один раз за запуск (при выходе в меню из паузы сцена перезагружается - вступления уже не будет).
public class StudioSplash : MonoBehaviour
{
    [Header("Логотип")]
    public string studioName = "ON THE KNEE";
    public string subtitle = "STUDIO";
    public Font titleFont;
    public Font subtitleFont;
    public Color titleColor = new Color(0.9f, 0.87f, 0.82f);
    public Color accentColor = new Color(0.72f, 0.04f, 0.03f);

    // Какой финал игрок видел в прошлый раз: 0 - ещё ни одного, 1 - Спасение, 2 - Чужая, 3 - Петля
    public const string LastEndingKey = "Kontora.LastEnding";
    private int lastEnding;
    private RawImage faceFlash;
    private Camera revealCam;
    private RenderTexture revealTexture;
    private GameObject revealBody, revealRig;
    private Transform revealHead;
    private float twitch, twitchTarget, twitchUntil, nextTwitch;

    [Header("Предупреждение 18+")]
    public bool showDisclaimer = true;
    public Font headerFont;
    public Font bodyFont;
    public float disclaimerMinTime = 2.5f;   // раньше пропустить нельзя
    public float disclaimerMaxTime = 15f;    // потом идёт дальше сам

    [TextArea(2, 4)] public string[] headersRu =
    {
        "ЮРИДИЧЕСКИЙ ОТКАЗ ОТ ОТВЕТСТВЕННОСТИ",
        "ПРЕДУПРЕЖДЕНИЕ О ЗДОРОВЬЕ",
        "ОБ ИДЕОЛОГИИ И СОДЕРЖАНИИ"
    };

    [TextArea(4, 12)] public string[] bodiesRu =
    {
        "«КОНТОРА» — художественное произведение. Все персонажи, события, места и организации, показанные в игре, вымышлены. Любое сходство с реально существующими людьми (живыми или умершими), событиями, местами или организациями случайно. Разработчики не несут ответственности за действия, совершённые под влиянием игры.",
        "Игра содержит сцены насилия, внезапные пугающие моменты, резкие громкие звуки, мерцающий свет и вспышки, которые у некоторых людей могут вызвать эпилептический приступ. Не рекомендуется людям с эпилепсией, заболеваниями сердца, повышенной тревожностью, беременным и лицам младше 18 лет. Если во время игры вы почувствовали головокружение, тошноту, судороги или иное недомогание, немедленно прекратите игру и обратитесь к врачу. Вы играете на свой страх и риск: разработчики не несут ответственности за ваше физическое и психическое состояние во время и после игры.",
        "Сатанинская символика, оккультные ритуалы, употребление наркотических веществ и связанные с ними образы использованы исключительно как художественные элементы жанра хоррор. Разработчики не поддерживают, не пропагандируют и не одобряют сатанизм, оккультизм, деятельность каких-либо религиозных, деструктивных или запрещённых организаций и сект, а также употребление наркотиков, и никак с ними не связаны."
    };

    [TextArea(2, 4)] public string[] headersEn =
    {
        "LEGAL DISCLAIMER",
        "HEALTH WARNING",
        "ABOUT IDEOLOGY AND CONTENT"
    };

    [TextArea(4, 12)] public string[] bodiesEn =
    {
        "\"KONTORA\" is a work of fiction. All characters, events, places and organizations depicted in the game are fictional. Any resemblance to actual persons (living or dead), events, places or organizations is purely coincidental. The developers are not responsible for any actions taken under the influence of the game.",
        "The game contains violence, jump scares, sudden loud sounds, flickering and flashing lights that may trigger epileptic seizures in some people. Not recommended for people with epilepsy, heart conditions or anxiety disorders, pregnant women, or anyone under 18. If you experience dizziness, nausea, seizures or any other discomfort, stop playing immediately and consult a doctor. You play at your own risk: the developers are not responsible for your physical or mental condition during or after playing.",
        "Satanic symbols, occult rituals, drug use and related imagery are used solely as artistic elements of the horror genre. The developers do not support, promote or endorse Satanism, occultism, any religious, destructive or banned organizations and cults, or drug use, and are not affiliated with them in any way."
    };

    public string copyright = "© 2026 On the Knee STUDIO. Все права защищены. All rights reserved.";
    public string pressAnyKey = "Нажмите любую клавишу, чтобы продолжить";

    [Header("Меню")]
    public MainMenuController menu;

    [Header("Звук (если пусто - генерируется)")]
    public AudioClip sting;
    [Range(0, 1)] public float volume = 0.9f;

    private static bool shownThisLaunch;

    private Image blackout;
    private CanvasGroup disclaimer;
    private Text continueHint;
    private RectTransform logo;
    private Text title, titleRed, titleCyan, sub;
    private Image line;
    private RawImage grain;
    private AudioSource audioSource;
    private AudioClip typeClick;
    private bool skip;

    void Awake()
    {
        if (shownThisLaunch)
        {
            Destroy(gameObject);
            return;
        }
        shownThisLaunch = true;
        Build();
    }

    IEnumerator Start()
    {
        if (menu == null) menu = FindAnyObjectByType<MainMenuController>();
        // MainMenuController включает музыку меню в своём Start - ждём кадр и глушим до конца вступления
        yield return null;
        if (menu != null && menu.menuMusic != null) menu.menuMusic.Stop();

        if (showDisclaimer) yield return DisclaimerPhase();
        yield return LogoPhase();
    }

    void Update()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || VRSupport.AnyDown) skip = true;

        // Зерно плёнки
        if (grain != null) grain.uvRect = new Rect(Random.value, Random.value, 2.5f, 1.4f);
    }

    // ---------- 1. Предупреждение ----------

    IEnumerator DisclaimerPhase()
    {
        disclaimer.gameObject.SetActive(true);
        disclaimer.alpha = 0f;
        continueHint.enabled = false;
        yield return Fade(0f, 1f, 0.8f, a => disclaimer.alpha = a);

        float shown = 0f;
        skip = false;
        while (shown < disclaimerMaxTime)
        {
            shown += Time.unscaledDeltaTime;
            bool canContinue = shown >= disclaimerMinTime;
            continueHint.enabled = canContinue;
            if (canContinue)
            {
                float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(shown * 3f));
                continueHint.color = new Color(1f, 1f, 1f, pulse * 0.8f);
                if (skip) break;
            }
            else skip = false; // нажатия до минимального времени не считаются
            yield return null;
        }

        yield return Fade(1f, 0f, 0.6f, a => disclaimer.alpha = a);
        disclaimer.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(0.3f);
        skip = false;
    }

    // ---------- 2. Логотип ----------

    IEnumerator LogoPhase()
    {
        logo.gameObject.SetActive(true);
        SetLogoAlpha(0f);
        line.rectTransform.sizeDelta = new Vector2(0f, 3f);

        // Темнота, нарастает синтезатор
        lastEnding = PlayerPrefs.GetInt(LastEndingKey, 0);
        bool haunted = lastEnding >= 2;   // игрок уже видел плохой финал: заставка это помнит
        string subtitleText = lastEnding == 3 ? "ТЫ ВЕРНУЛСЯ" : lastEnding == 2 ? "ОНА ВСЁ ЕЩЁ С ТОБОЙ" : subtitle;
        audioSource.Play();
        yield return Wait(1.0f);

        // Удар: надпись вспыхивает, как неисправная лампа
        float[] flicker = { 1f, 0f, 0.8f, 0.1f, 1f, 0.3f, 1f };
        foreach (float a in flicker)
        {
            if (skip) break;
            SetTitleAlpha(a);
            Glitch(a > 0.5f ? 8f : 0f);
            yield return Wait(Random.Range(0.03f, 0.08f));
        }
        SetTitleAlpha(1f);
        Glitch(0f);

        // Линия вырастает из центра, STUDIO печатается по буквам с щелчками машинки
        float t = 0f;
        int typed = 0;
        sub.color = WithAlpha(accentColor, 1f);
        while (t < 1f && !skip)
        {
            t += Time.unscaledDeltaTime / 1.1f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            line.rectTransform.sizeDelta = new Vector2(600f * e, 3f);
            line.color = WithAlpha(accentColor, e);
            int letters = Mathf.FloorToInt(Mathf.Clamp01(t * 1.25f - 0.15f) * subtitleText.Length);
            if (letters > typed)
            {
                typed = letters;
                audioSource.PlayOneShot(typeClick, 0.55f);
            }
            sub.text = Spaced(subtitleText.Substring(0, typed));
            yield return null;
        }
        line.rectTransform.sizeDelta = new Vector2(600f, 3f);
        line.color = accentColor;
        sub.text = Spaced(subtitleText);

        // Держим надпись: медленное приближение и редкие глитчи
        float hold = 2.4f, h = 0f, nextGlitch = 0.6f;
        Coroutine burst = null;
        while (h < hold && !skip)
        {
            h += Time.unscaledDeltaTime;
            logo.localScale = Vector3.one * (1f + 0.03f * h / hold);
            if (h >= nextGlitch)
            {
                nextGlitch = h + (haunted ? Random.Range(0.2f, 0.5f) : Random.Range(0.5f, 1.1f));
                burst = StartCoroutine(GlitchBurst());
            }
            yield return null;
        }
        if (burst != null) StopCoroutine(burst);

        if (skip)
        {
            yield return Fade(1f, 0f, 0.25f, SetLogoAlpha);
        }
        else
        {
            yield return LogoBreaks(haunted);
        }
        yield return new WaitForSecondsRealtime(skip ? 0.05f : 0.4f);

        // 3. Проявляем меню
        if (menu != null && menu.menuMusic != null) menu.menuMusic.Play();
        StartCoroutine(FadeOutSting(skip ? 0.4f : 1.5f));
        yield return Fade(1f, 0f, 0.8f, a => blackout.color = new Color(0f, 0f, 0f, a));
        while (audioSource.isPlaying) yield return null;
        Destroy(gameObject);
    }

    // Надпись разваливается, звук проседает, и на долю секунды из темноты смотрит лицо
    IEnumerator LogoBreaks(bool haunted)
    {
        const string junk = "#%/\\|?!_";
        float time = haunted ? 1.1f : 0.75f;
        char[] letters = studioName.ToCharArray();
        for (float t = 0f; t < time && !skip; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            audioSource.pitch = Mathf.Lerp(1f, 0.3f, k * k);
            Glitch(Mathf.Lerp(6f, 34f, k));
            SetTitleAlpha(Random.value < k * 0.55f ? 0.08f : 1f);
            for (int i = 0; i < letters.Length; i++)
                letters[i] = letters[i] != ' ' && Random.value < k * k * 0.7f ? junk[Random.Range(0, junk.Length)] : studioName[i];
            title.text = titleRed.text = titleCyan.text = new string(letters);
            yield return null;
        }
        title.text = titleRed.text = titleCyan.text = studioName;
        Glitch(0f);
        SetLogoAlpha(0f);

        if (skip) { audioSource.pitch = 1f; yield break; }
        // Гул почти стих. Из темноты быстро проступает живая голова, потом резкий обрыв
        audioSource.volume = volume * 0.3f;
        BuildReveal();
        yield return Wait(haunted ? 0.35f : 0.2f);   // камера успевает отрисовать первые кадры в темноте
        if (!skip && revealCam != null)
        {
            float appear = haunted ? 1.5f : 1f;
            Vector3 far = revealCam.transform.position;
            Vector3 near = far + revealCam.transform.forward * 0.13f;
            faceFlash.color = new Color(1f, 1f, 1f, 0f);
            faceFlash.gameObject.SetActive(true);
            for (float t = 0f; t < appear && !skip; t += Time.unscaledDeltaTime)
            {
                float k = t / appear;
                faceFlash.color = new Color(1f, 1f, 1f, k * k);
                revealCam.transform.position = Vector3.Lerp(far, near, k);
                audioSource.volume = volume * Mathf.Lerp(0.3f, 0.75f, k);
                yield return null;
            }
            yield return Wait(0.15f);
            faceFlash.gameObject.SetActive(false);
            if (!skip) audioSource.PlayOneShot(typeClick, 1f);
        }
        ClearReveal();
        audioSource.pitch = 1f;
        audioSource.volume = volume;
    }

    // Голова бабки рисуется вживую вдали от сцены и выводится на экран
    void BuildReveal()
    {
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        if (hm == null || hm.realGhost == null) return;

        revealBody = GrannyJumpscare.MakeCopy(hm.realGhost);
        revealBody.transform.SetPositionAndRotation(new Vector3(0f, -300f, 0f), Quaternion.identity);
        revealBody.SetActive(true);
        var anim = revealBody.GetComponent<Animator>();
        anim.Play("скример", 0, 0.1f);
        anim.speed = 0.3f;   // еле заметно тянется вперёд
        anim.Update(0f);
        revealHead = anim.GetBoneTransform(HumanBodyBones.Head);

        revealRig = new GameObject("Splash Reveal Rig");
        revealRig.transform.position = revealHead.position;
        revealCam = new GameObject("Reveal Camera").AddComponent<Camera>();
        revealCam.transform.SetParent(revealRig.transform, false);
        revealCam.transform.SetPositionAndRotation(revealHead.position + Vector3.forward * 0.55f + Vector3.up * 0.1f, Quaternion.LookRotation(Vector3.back));
        var hd = revealCam.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        hd.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = Color.black;
        hd.volumeLayerMask = 0;
        revealCam.fieldOfView = 42f;
        revealCam.nearClipPlane = 0.03f;
        revealCam.farClipPlane = 6f;
        revealTexture = new RenderTexture(1280, 720, 24);
        revealCam.targetTexture = revealTexture;
        faceFlash.texture = revealTexture;

        AddRevealLight(new Vector3(0.08f, -0.35f, 0.35f), 3f, new Color(0.68f, 0.85f, 0.75f));
        AddRevealLight(new Vector3(-0.35f, 0.05f, 0.2f), 0.8f, new Color(0.7f, 0.15f, 0.12f));
    }

    void AddRevealLight(Vector3 offset, float lumens, Color color)
    {
        var go = new GameObject("Reveal Light");
        go.transform.SetParent(revealRig.transform, false);
        go.transform.localPosition = offset;
        var l = go.AddComponent<Light>();
        go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        l.type = LightType.Point;
        l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        l.intensity = lumens;
        l.range = 3f;
        l.color = color;
        l.shadows = LightShadows.None;
    }

    void ClearReveal()
    {
        if (revealBody != null) Destroy(revealBody);
        if (revealRig != null) Destroy(revealRig);
        if (revealTexture != null) revealTexture.Release();
        revealHead = null;
    }

    void OnDestroy() => ClearReveal();

    // Кости после Animator: голова смотрит в камеру, наклонена и дёргается
    void LateUpdate()
    {
        if (revealHead == null || revealCam == null) return;
        float now = Time.unscaledTime;
        if (now >= nextTwitch)
        {
            nextTwitch = now + Random.Range(0.3f, 0.7f);
            twitchUntil = now + 0.1f;
            twitchTarget = Random.value < 0.5f ? -32f : 32f;
        }
        twitch = Mathf.MoveTowards(twitch, now < twitchUntil ? twitchTarget : 0f, 700f * Time.unscaledDeltaTime);

        Vector3 toCam = (revealCam.transform.position - revealHead.position).normalized;
        Quaternion look = Quaternion.FromToRotation(revealHead.forward, toCam);
        float roll = 10f + Mathf.Sin(now * 2.6f) * 7f + twitch;
        revealHead.rotation = Quaternion.AngleAxis(roll, toCam) * look * revealHead.rotation;
    }

    IEnumerator FadeOutSting(float time)
    {
        float start = audioSource.volume;
        yield return Fade(1f, 0f, time, k => audioSource.volume = start * k);
        audioSource.Stop();
    }

    IEnumerator GlitchBurst()
    {
        for (int i = 0; i < 3; i++)
        {
            Glitch(Random.Range(4f, 10f));
            SetTitleAlpha(Random.Range(0.6f, 1f));
            yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.06f));
        }
        Glitch(0f);
        SetTitleAlpha(1f);
    }

    // Расслоение на красный/голубой и дрожание по горизонтали
    void Glitch(float amount)
    {
        float jx = amount > 0f ? Random.Range(-amount, amount) * 0.6f : 0f;
        title.rectTransform.anchoredPosition = new Vector2(jx, 30f);
        titleRed.rectTransform.anchoredPosition = new Vector2(jx - amount, 30f + Random.Range(-1f, 1f) * amount * 0.2f);
        titleCyan.rectTransform.anchoredPosition = new Vector2(jx + amount, 30f);
        titleRed.enabled = titleCyan.enabled = amount > 0f && title.color.a > 0.05f;
    }

    IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end && !skip) yield return null;
    }

    IEnumerator Fade(float from, float to, float time, System.Action<float> apply)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / time;
            apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        apply(to);
    }

    void SetTitleAlpha(float a)
    {
        title.color = WithAlpha(titleColor, a);
        titleRed.color = new Color(0.45f, 0.03f, 0.02f, 0.75f * a);   // засохшая кровь, а не неон
        titleCyan.color = new Color(0.25f, 0.4f, 0.3f, 0.55f * a);    // больничный зелёный, как лампы в коридорах
    }

    void SetLogoAlpha(float a)
    {
        SetTitleAlpha(a);
        sub.color = WithAlpha(accentColor, a);
        line.color = WithAlpha(accentColor, a);
    }

    // Тёмное скругление: видно только овал с лицом
    static Texture2D MakeFaceMask()
    {
        const int size = 256;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.55f;
                float d = Mathf.Sqrt(dx * dx * 2.2f + dy * dy * 1.1f);
                px[y * size + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.46f, d)));
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    // ---------- Построение интерфейса ----------

    void Build()
    {
        var canvasGo = new GameObject("Splash Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Чёрный фон перекрывает меню и ловит клики, пока идёт вступление
        blackout = NewGraphic<Image>("Blackout", canvasGo.transform);
        Stretch(blackout.rectTransform);
        blackout.color = Color.black;
        blackout.raycastTarget = true;

        BuildDisclaimer(canvasGo.transform);
        BuildLogo(canvasGo.transform);

        grain = NewGraphic<RawImage>("Grain", canvasGo.transform);
        Stretch(grain.rectTransform);
        grain.texture = MakeNoise();
        grain.color = new Color(1f, 1f, 1f, 0.01f); // проект в линейном цвете: малая альфа уже заметна

        var vignette = NewGraphic<RawImage>("Vignette", canvasGo.transform);
        Stretch(vignette.rectTransform);
        vignette.texture = MakeVignette();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = sting != null ? sting : MakeSting();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
        typeClick = MakeTypeClick();
    }

    void BuildLogo(Transform parent)
    {
        logo = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
        logo.SetParent(parent, false);
        logo.sizeDelta = new Vector2(1600, 400);

        Font f1 = titleFont != null ? titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font f2 = subtitleFont != null ? subtitleFont : f1;
        titleRed = NewText("Title Red", logo, f1, 150, studioName, TextAnchor.MiddleCenter, new Vector2(1600, 220));
        titleCyan = NewText("Title Cyan", logo, f1, 150, studioName, TextAnchor.MiddleCenter, new Vector2(1600, 220));
        title = NewText("Title", logo, f1, 150, studioName, TextAnchor.MiddleCenter, new Vector2(1600, 220));
        foreach (var t in new[] { titleRed, titleCyan, title }) t.rectTransform.anchoredPosition = new Vector2(0f, 30f);

        line = NewGraphic<Image>("Line", logo);
        line.rectTransform.anchoredPosition = new Vector2(0f, -70f);

        sub = NewText("Studio", logo, f2, 52, "", TextAnchor.MiddleCenter, new Vector2(1600, 80));
        sub.rectTransform.anchoredPosition = new Vector2(0f, -122f);
        logo.gameObject.SetActive(false);

        faceFlash = NewGraphic<RawImage>("Face Flash", parent);
        Stretch(faceFlash.rectTransform);
        var mask = NewGraphic<RawImage>("Face Dark Edge", faceFlash.transform);
        Stretch(mask.rectTransform);
        mask.texture = MakeFaceMask();
        faceFlash.gameObject.SetActive(false);
    }

    void BuildDisclaimer(Transform parent)
    {
        var root = new GameObject("Disclaimer", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform);
        disclaimer = root.GetComponent<CanvasGroup>();
        disclaimer.interactable = disclaimer.blocksRaycasts = false;

        Font head = headerFont != null ? headerFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font body = bodyFont != null ? bodyFont : head;
        Color red = new Color(0.85f, 0.07f, 0.05f);

        // Знак 18+ сверху
        var badge = NewGraphic<Image>("Badge 18+", root.transform);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        badge.rectTransform.sizeDelta = new Vector2(110, 110);
        badge.rectTransform.anchoredPosition = new Vector2(0f, -95f);
        badge.color = red;
        var badgeInner = NewGraphic<Image>("Inner", badge.transform);
        badgeInner.rectTransform.sizeDelta = new Vector2(98, 98);
        badgeInner.color = Color.black;
        NewText("18+", badge.transform, head, 54, "18+", TextAnchor.MiddleCenter, new Vector2(110, 110)).color = red;

        var age = NewText("Age", root.transform, body, 22, "ТОЛЬКО ДЛЯ ЛИЦ СТАРШЕ 18 ЛЕТ  ·  FOR ADULTS ONLY (18+)", TextAnchor.MiddleCenter, new Vector2(1400, 40));
        age.rectTransform.anchorMin = age.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        age.rectTransform.anchoredPosition = new Vector2(0f, -175f);
        age.color = new Color(1f, 1f, 1f, 0.75f);

        BuildColumn("Column RU", root.transform, -455f, headersRu, bodiesRu, head, body, red);
        BuildColumn("Column EN", root.transform, 455f, headersEn, bodiesEn, head, body, red);

        var copy = NewText("Copyright", root.transform, body, 18, copyright, TextAnchor.MiddleCenter, new Vector2(1600, 30));
        copy.rectTransform.anchorMin = copy.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        copy.rectTransform.anchoredPosition = new Vector2(0f, 85f);
        copy.color = new Color(1f, 1f, 1f, 0.45f);

        continueHint = NewText("Continue", root.transform, head, 26, pressAnyKey, TextAnchor.MiddleCenter, new Vector2(1600, 40));
        continueHint.rectTransform.anchorMin = continueHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        continueHint.rectTransform.anchoredPosition = new Vector2(0f, 42f);

        root.SetActive(false);
    }

    void BuildColumn(string name, Transform parent, float x, string[] headers, string[] bodies, Font head, Font body, Color red)
    {
        var col = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        col.SetParent(parent, false);
        col.anchorMin = col.anchorMax = new Vector2(0.5f, 1f);
        col.pivot = new Vector2(0.5f, 1f);
        col.sizeDelta = new Vector2(820, 760);
        col.anchoredPosition = new Vector2(x, -220f);
        var layout = col.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < Mathf.Min(headers.Length, bodies.Length); i++)
        {
            var h = NewText("Header " + (i + 1), col, head, 30, headers[i], TextAnchor.LowerLeft, Vector2.zero);
            h.color = red;
            var b = NewText("Body " + (i + 1), col, body, 21, bodies[i], TextAnchor.UpperLeft, Vector2.zero);
            b.color = new Color(0.92f, 0.92f, 0.92f, 0.92f);
            b.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.lineSpacing = 1.1f;
            if (i < headers.Length - 1)
            {
                var gap = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
                gap.transform.SetParent(col, false);
                gap.GetComponent<LayoutElement>().minHeight = 18;
            }
        }
    }

    static Text NewText(string name, Transform parent, Font font, int size, string text, TextAnchor align, Vector2 sizeDelta)
    {
        var t = NewGraphic<Text>(name, parent);
        t.rectTransform.sizeDelta = sizeDelta;
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow; // иначе высокий шрифт молча обрезается целиком
        t.text = text;
        return t;
    }

    static T NewGraphic<T>(string name, Transform parent) where T : Graphic
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

    static Texture2D MakeNoise()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[size * size];
        var rnd = new System.Random(5);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rnd.Next(256);
            px[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeVignette()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x / (size - 1f) * 2f - 1f, dy = y / (size - 1f) * 2f - 1f;
                float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.9f);
                px[y * size + x] = new Color(0f, 0f, 0f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------- Звук ----------

    // Тяжёлый аналоговый синтезатор: за секунду нарастает гул с открывающимся фильтром,
    // на ударе - суб-бас, минорный аккорд расстроенных пил, холодный металлический призвук
    // и длинный хвост реверберации.
    static AudioClip MakeSting()
    {
        const int rate = 44100;
        const float hitAt = 1.0f;
        int n = (int)(rate * 7f);
        var dry = new float[n];
        var rnd = new System.Random(9);

        // Аккорд ля минор в низком регистре: A1, E2, A2, C3 (+ немного расстройки на каждую ноту)
        float[] notes = { 55f, 82.41f, 110f, 130.81f };
        float[] detune = { -0.006f, 0.004f, 0.009f };
        var phases = new float[notes.Length * detune.Length];
        float lp1 = 0f, lp2 = 0f, noiseLp = 0f, subPhase = 0f;
        // Металлические неровные обертоны (как удар по листу железа)
        float[] metal = { 311f, 523f, 847f, 1393f, 2111f };
        var metalPhase = new float[metal.Length];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float k = t - hitAt;
            float white = (float)rnd.NextDouble() * 2f - 1f;

            // Пилы аккорда
            float saw = 0f;
            for (int a = 0; a < notes.Length; a++)
                for (int d = 0; d < detune.Length; d++)
                {
                    int idx = a * detune.Length + d;
                    // перед ударом аккорд поднимается снизу на полтона - "затягивание"
                    float bend = t < hitAt ? Mathf.Lerp(0.94f, 1f, t / hitAt) : 1f;
                    phases[idx] = Mathf.Repeat(phases[idx] + notes[a] * (1f + detune[d]) * bend / rate, 1f);
                    saw += phases[idx] * 2f - 1f;
                }
            saw /= notes.Length * detune.Length;

            // Фильтр: до удара медленно открывается, на ударе распахивается и медленно закрывается
            float cutoff = t < hitAt
                ? Mathf.Lerp(0.004f, 0.03f, Mathf.Pow(t / hitAt, 2f))
                : Mathf.Lerp(0.02f, 0.18f, Mathf.Exp(-k * 1.6f));
            lp1 += (saw - lp1) * cutoff;
            lp2 += (lp1 - lp2) * cutoff;
            float ampPad = t < hitAt ? Mathf.Pow(t / hitAt, 2.2f) * 0.6f : Mathf.Exp(-k * 0.55f) * (1f + 0.6f * Mathf.Exp(-k * 6f));
            float pad = lp2 * ampPad * 2.2f;

            float s = pad;

            // Шорох-всасывание до удара
            if (t < hitAt)
            {
                noiseLp += (white - noiseLp) * Mathf.Lerp(0.02f, 0.4f, t / hitAt);
                s += noiseLp * Mathf.Pow(t / hitAt, 3f) * 0.35f;
            }
            else
            {
                // Суб-бас удар 60 -> 30 Гц
                float f = Mathf.Lerp(60f, 30f, Mathf.Clamp01(k / 1.5f));
                subPhase += f / rate;
                s += Mathf.Sin(subPhase * 2f * Mathf.PI) * Mathf.Exp(-k * 1.1f) * 0.9f;

                // Металлический призвук
                float m = 0f;
                for (int j = 0; j < metal.Length; j++)
                {
                    metalPhase[j] += metal[j] / rate;
                    m += Mathf.Sin(metalPhase[j] * 2f * Mathf.PI) * Mathf.Exp(-k * (1.2f + j * 0.7f)) / (j + 1);
                }
                s += m * 0.18f;

                // Щелчок-транзиент
                if (k < 0.012f) s += white * (1f - k / 0.012f) * 0.5f;
            }
            dry[i] = s;
        }

        float[] wet = Reverb(dry, rate);
        var data = new float[n];
        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            data[i] = dry[i] * 0.75f + wet[i] * 0.55f;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        // Плавное затухание в конце и нормализация
        int tail = rate;
        for (int i = n - tail; i < n; i++) data[i] *= (n - i) / (float)tail;
        if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;

        var clip = AudioClip.Create("Studio Sting", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Реверберация Шрёдера: 4 гребенчатых фильтра параллельно + 2 всепропускающих последовательно
    static float[] Reverb(float[] input, int rate)
    {
        int n = input.Length;
        var output = new float[n];
        float[] combMs = { 29.7f, 37.1f, 41.1f, 43.7f };
        foreach (float ms in combMs)
        {
            int delay = (int)(ms * 0.001f * rate * 1.6f);
            var buf = new float[delay];
            int pos = 0;
            float damp = 0f;
            for (int i = 0; i < n; i++)
            {
                float y = buf[pos];
                damp = y * 0.6f + damp * 0.4f;         // глушим верха в хвосте
                buf[pos] = input[i] + damp * 0.86f;
                pos = (pos + 1) % delay;
                output[i] += y * 0.25f;
            }
        }
        foreach (float ms in new[] { 5f, 1.7f })
        {
            int delay = (int)(ms * 0.001f * rate);
            var buf = new float[delay];
            int pos = 0;
            for (int i = 0; i < n; i++)
            {
                float b = buf[pos];
                float y = -output[i] * 0.5f + b;
                buf[pos] = output[i] + b * 0.5f;
                pos = (pos + 1) % delay;
                output[i] = y;
            }
        }
        return output;
    }

    // Щелчок клавиши печатной машинки
    static AudioClip MakeTypeClick()
    {
        const int rate = 44100;
        int n = rate / 12;
        var data = new float[n];
        var rnd = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.5f;
            float strike = (white - lp) * Mathf.Exp(-t * 180f);          // удар литеры
            float body = Mathf.Sin(t * 2f * Mathf.PI * 190f) * Mathf.Exp(-t * 60f) * 0.5f; // глухой корпус
            data[i] = (strike + body) * 0.8f;
        }
        var clip = AudioClip.Create("Type Click", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
