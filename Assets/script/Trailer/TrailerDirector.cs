using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Постановка трейлера "КОНТОРА" прямо в игре: свои камера и звук, проезды по сцене,
// интертитры, ускоряющийся монтаж, скример и название. Запускается из Tools > Trailer > Record.
public class TrailerDirector : MonoBehaviour
{
    public TMP_FontAsset titleFont, textFont;
    public Material titleMaterial, textMaterial;
    public AudioClip heartbeat, scream, growl, momLine, addictLine, buzz;

    public bool Finished { get; private set; }

    private Camera cam;
    private Image fade, flash;
    private RectTransform barTop, barBottom;
    private TextMeshProUGUI card, small, rec;
    private AudioSource music, heart, voice, sfx;
    private GameObject cctvOverlay;
    private Transform camTarget;

    // ---------- Подготовка ----------

    public void Begin() => StartCoroutine(Run());

    void Setup()
    {
        // Убираем игрока, меню и интерфейс: в кадре только мир
        var pc = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null) pc.gameObject.SetActive(false);
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.transform.parent == null && c.gameObject != gameObject) c.gameObject.SetActive(false);
        var menuCam = GameObject.Find("MenuCamera"); if (menuCam != null) menuCam.SetActive(false);
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) l.enabled = false;

        var cg = new GameObject("Trailer Camera");
        cg.transform.SetParent(transform, false);
        cam = cg.AddComponent<Camera>();
        cg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        cg.AddComponent<AudioListener>();
        cam.fieldOfView = 45f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 250f;

        // Трейлеру нужно чуть больше света, чем игре: прибавка экспозиции только для этой камеры
        var vg = new GameObject("Trailer Exposure"); vg.transform.SetParent(transform, false); vg.layer = 31;
        var vol = vg.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true; vol.priority = 500f;
        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var ex = prof.Add<UnityEngine.Rendering.HighDefinition.Exposure>(); ex.compensation.Override(1.3f);
        vol.sharedProfile = prof;
        var hdCam = cg.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        hdCam.volumeLayerMask = 1 | (1 << 31);

        AudioListener.volume = 1f;
        music = NewSource(0.9f); heart = NewSource(0.8f); voice = NewSource(1f); sfx = NewSource(1f);
        music.clip = MakeDrone(); music.loop = true; music.volume = 0f; music.Play();
        heart.clip = heartbeat; heart.loop = true;

        BuildOverlay();
    }

    AudioSource NewSource(float vol)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false; s.spatialBlend = 0f; s.volume = vol; s.ignoreListenerPause = true;
        return s;
    }

    // ---------- Монтаж ----------

    IEnumerator Run()
    {
        Setup();
        yield return null;
        yield return Fade(1f, 1f, 0.01f);

        // 0. Студия
        StartCoroutine(Volume(music, 0.55f, 4f));
        yield return Card("On the Knee STUDIO\n<size=60%>представляет</size>", 3.2f, true);

        // 1. Ночь, вагончик охраны вдали
        var t = Terrain.activeTerrain;
        Vector3 G(float x, float z, float h) => new Vector3(x, t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y + h, z);
        voice.clip = momLine;
        yield return Shot(G(-40f, -48f, 1.6f), G(-33f, -42f, 1.5f), new Vector3(-24.3f, 2.0f, -36.1f), 6.5f, 1.2f, () => voice.PlayDelayed(1.4f));

        // 2. Внутри вагончика: телефон, телевизор
        yield return Shot(new Vector3(-26.6f, 1.75f, -34.6f), new Vector3(-24.6f, 1.55f, -35.4f), new Vector3(-21.8f, 1.1f, -37.6f), 5f, 0.6f);
        yield return Card("Обычная ночная смена.", 2.6f);

        // 3. Здание, мигающая лампа над входом
        yield return Shot(G(-3.5f, 26f, 1.7f), G(-3.5f, 15f, 1.6f), new Vector3(-3.5f, 5.6f, 5.3f), 6f, 0.6f);

        // 4. Камера наблюдения: дверь открывается сама
        yield return Cctv("CAM 01   1 ЭТАЖ — ВХОД", 5.5f);
        yield return Card("Но ты здесь не один.", 2.6f);

        // 5. Коридор первого этажа, сердце начинает биться
        heart.volume = 0.5f; heart.pitch = 1f; heart.Play();
        yield return Shot(new Vector3(-0.5f, 5.3f, -5.3f), new Vector3(-9.5f, 5.3f, -5.3f), new Vector3(-20f, 5.1f, -5.3f), 5f, 0.4f);

        // 6. Наркоман в углу, кукла рядом
        voice.clip = addictLine;
        var npc = FindAnyObjectByType<NPCDialogueController>();
        Vector3 nPos = npc != null ? npc.transform.position : new Vector3(13.7f, 7.4f, 0.1f);
        // Холодный слабый свет сбоку, будто из окна, - иначе в углу ничего не видно
        var fillGo = new GameObject("NPC Fill"); fillGo.transform.SetParent(transform, false);
        fillGo.transform.position = nPos + new Vector3(-1.6f, 1.9f, -1.4f);
        var fill = fillGo.AddComponent<Light>(); fillGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>().volumetricDimmer = 0f;
        fill.type = LightType.Point; fill.range = 5f; fill.intensity = 450f; fill.color = new Color(0.62f, 0.72f, 1f); fill.shadows = LightShadows.Soft;
        yield return Shot(nPos + new Vector3(-2.6f, 1.5f, -1.8f), nPos + new Vector3(-1.5f, 1.25f, -1.0f), nPos + new Vector3(0f, 0.75f, 0.4f), 5f, 0.3f, () => voice.PlayDelayed(0.6f));
        Destroy(fillGo);
        yield return Card("Они что-то призвали.", 2.6f);

        // 7. Пентаграмма и свечи
        foreach (var c in FindObjectsByType<CandleInteract>(FindObjectsSortMode.None)) if (c.fireEffect != null) c.fireEffect.SetActive(true);
        var pent = FindAnyObjectByType<Pentagram>();
        Vector3 pc = pent != null ? pent.transform.position : new Vector3(3.7f, 12f, -13.4f);
        heart.pitch = 1.25f; heart.volume = 0.7f;
        yield return Shot(pc + new Vector3(-0.2f, 3.0f, 2.6f), pc + new Vector3(0.2f, 2.2f, 1.6f), pc, 4.5f, 0.3f);

        // 8. Быстрый монтаж: бабка, плакат, кукла, бабка ближе
        var granny = PrepareGranny();
        heart.pitch = 1.55f; heart.volume = 0.9f;
        StartCoroutine(Volume(music, 0.9f, 2f));
        Vector3 corridorCam = new Vector3(-13.5f, 9.2f, -5.2f);
        yield return GrannyShot(granny, corridorCam, new Vector3(-24.5f, 7.84f, -5.2f), new Vector3(-23.3f, 7.84f, -5.2f), 1.6f);
        Hit();
        var poster = GameObject.Find("Ritual Poster");
        // лицевая сторона квада плаката смотрит против его forward - в комнату
        if (poster != null) yield return Shot(poster.transform.position - poster.transform.forward * 1.3f + Vector3.up * 0.1f, poster.transform.position - poster.transform.forward * 0.9f, poster.transform.position, 0.9f, 0f);
        Hit();
        var doll = GameObject.Find("Voodoo Doll");
        if (doll != null) yield return Shot(doll.transform.position + new Vector3(-0.55f, 0.25f, -0.45f), doll.transform.position + new Vector3(-0.38f, 0.2f, -0.32f), doll.transform.position + Vector3.up * 0.15f, 0.8f, 0f);
        Hit();
        yield return GrannyShot(granny, corridorCam, new Vector3(-19.8f, 7.84f, -5.2f), new Vector3(-18.6f, 7.84f, -5.2f), 1.0f);
        Hit();
        yield return Flash(0.12f);
        yield return GrannyShot(granny, corridorCam, new Vector3(-16.6f, 7.84f, -5.2f), new Vector3(-15.9f, 7.84f, -5.2f), 0.7f);

        // 9. Скример
        yield return Screamer(granny, corridorCam);

        // 10. Тишина и название
        music.Stop(); heart.Stop();
        fade.color = Color.black;
        yield return new WaitForSeconds(1.4f);
        sfx.PlayOneShot(MakeBoom(), 1f);
        yield return Title(6.5f);
        Finished = true;
    }

    // ---------- Кадры ----------

    IEnumerator Shot(Vector3 from, Vector3 to, Vector3 look, float time, float fadeTime, System.Action onStart = null)
    {
        cam.transform.position = from; cam.transform.LookAt(look);
        onStart?.Invoke();
        if (fadeTime > 0f) StartCoroutine(Fade(1f, 0f, fadeTime)); else fade.color = Color.clear;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            cam.transform.position = Vector3.Lerp(from, to, k);
            var dir = look - cam.transform.position;
            cam.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 0.3f, Mathf.Sin(t * 0.5f) * 0.4f, 0f);
            if (fadeTime > 0f && t > time - fadeTime && fade.color.a < 0.01f) StartCoroutine(Fade(0f, 1f, fadeTime));
            yield return null;
        }
        if (fadeTime > 0f) fade.color = Color.black;
    }

    IEnumerator Cctv(string label, float time)
    {
        var cctv = FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include);
        CCTVCamera camEntrance = null;
        foreach (var c in cctv) if (c.label == "CAM 01") camEntrance = c;
        if (camEntrance == null) yield break;

        // Смотрим через камеру наблюдения (ночной режим на её слое) и включаем ИК-подсветку
        var hd = cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        var oldMask = hd.volumeLayerMask; float oldFov = cam.fieldOfView;
        hd.volumeLayerMask = camEntrance.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>().volumeLayerMask;
        cam.fieldOfView = camEntrance.Cam.fieldOfView;
        cam.transform.SetPositionAndRotation(camEntrance.transform.position, camEntrance.transform.rotation);
        if (camEntrance.irLight != null) camEntrance.irLight.enabled = true;
        cctvOverlay.SetActive(true);
        rec.text = "● REC      " + label;
        sfx.PlayOneShot(MakeHiss(), 0.35f);
        fade.color = Color.clear;

        float t = 0f; bool opened = false;
        while (t < time)
        {
            t += Time.deltaTime;
            rec.alpha = Mathf.Repeat(t, 1.2f) < 0.8f ? 0.9f : 0.35f;
            if (!opened && t > 2.0f && camEntrance.doorToOpen != null) { opened = true; camEntrance.doorToOpen.Interact(); }
            // редкие сбои картинки
            cam.transform.position = camEntrance.transform.position + (Random.value < 0.03f ? Random.insideUnitSphere * 0.03f : Vector3.zero);
            yield return null;
        }
        cctvOverlay.SetActive(false);
        if (camEntrance.irLight != null) camEntrance.irLight.enabled = false;
        hd.volumeLayerMask = oldMask; cam.fieldOfView = oldFov;
        fade.color = Color.black;
    }

    GameObject PrepareGranny()
    {
        var chase = FindAnyObjectByType<GhostChase>(FindObjectsInactive.Include);
        if (chase == null) return null;
        var g = chase.gameObject;
        foreach (var mb in g.GetComponents<MonoBehaviour>()) mb.enabled = false;
        var agent = g.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false;
        g.SetActive(true);
        return g;
    }

    IEnumerator GrannyShot(GameObject g, Vector3 camPos, Vector3 from, Vector3 to, float time)
    {
        if (g == null) yield break;
        var an = g.GetComponent<Animator>();
        if (an != null) an.SetFloat("Speed", 1.6f);
        g.transform.rotation = Quaternion.LookRotation(new Vector3(camPos.x - from.x, 0f, camPos.z - from.z));
        cam.transform.position = camPos;
        fade.color = Color.clear;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            g.transform.position = Vector3.Lerp(from, to, t / time);
            cam.transform.LookAt(g.transform.position + Vector3.up * 1.4f);
            cam.transform.position = camPos + Random.insideUnitSphere * 0.006f;
            yield return null;
        }
    }

    IEnumerator Screamer(GameObject g, Vector3 camPos)
    {
        if (g == null) yield break;
        var an = g.GetComponent<Animator>();
        Vector3 start = g.transform.position;
        Vector3 end = camPos + (start - camPos).normalized * 0.9f; end.y = start.y;
        if (an != null) an.SetTrigger("Cream");
        sfx.PlayOneShot(scream, 1f);
        if (growl != null) sfx.PlayOneShot(growl, 0.6f);
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - t / 0.55f, 3f);
            g.transform.position = Vector3.Lerp(start, end, k);
            cam.transform.position = camPos + Random.insideUnitSphere * 0.05f * k;
            cam.transform.LookAt(g.transform.position + Vector3.up * 1.45f);
            cam.fieldOfView = Mathf.Lerp(45f, 58f, k);
            yield return null;
        }
        fade.color = Color.black;
        cam.fieldOfView = 45f;
    }

    // ---------- Тексты и экран ----------

    IEnumerator Card(string text, float time, bool studio = false)
    {
        fade.color = Color.black;
        card.text = text;
        card.font = studio ? textFont : textFont;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(Mathf.Min(t / 0.6f, (time - t) / 0.6f));
            card.alpha = a;
            card.rectTransform.localScale = Vector3.one * (1f + 0.04f * t / time);
            yield return null;
        }
        card.alpha = 0f;
    }

    IEnumerator Title(float time)
    {
        var title = NewText("Title", transform.GetChild(0), titleFont, titleMaterial, 170, "КОНТОРА", new Color(0.66f, 0.04f, 0.03f), new Vector2(0, 60), new Vector2(1600, 240));
        title.characterSpacing = 14; title.fontStyle = FontStyles.UpperCase;
        title.gameObject.AddComponent<MenuTitleDrips>();
        small.text = "Скоро\n<size=70%>On the Knee STUDIO</size>";
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            title.alpha = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((time - t) / 1.2f);
            small.alpha = Mathf.Clamp01((t - 1.5f) / 1.2f) * Mathf.Clamp01((time - t) / 1.2f);
            yield return null;
        }
    }

    void Hit()
    {
        sfx.PlayOneShot(MakeHit(), 0.9f);
    }

    IEnumerator Flash(float time)
    {
        flash.color = new Color(1f, 1f, 1f, 0.85f);
        yield return new WaitForSeconds(time);
        flash.color = Color.clear;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < time) { t += Time.deltaTime; fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / time)); yield return null; }
        fade.color = new Color(0, 0, 0, to);
    }

    IEnumerator Volume(AudioSource s, float to, float time)
    {
        float from = s.volume, t = 0f;
        while (t < time) { t += Time.deltaTime; s.volume = Mathf.Lerp(from, to, t / time); yield return null; }
    }

    void BuildOverlay()
    {
        var cgo = new GameObject("Trailer Overlay", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        cgo.transform.SetAsFirstSibling();
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;

        cctvOverlay = new GameObject("CCTV", typeof(RectTransform)); cctvOverlay.transform.SetParent(cgo.transform, false); Stretch((RectTransform)cctvOverlay.transform);
        rec = NewText("Rec", cctvOverlay.transform, textFont, textMaterial, 34, "", new Color(0.95f, 0.15f, 0.1f), new Vector2(-480, 360), new Vector2(900, 60));
        rec.alignment = TextAlignmentOptions.Left;
        cctvOverlay.SetActive(false);

        fade = NewImage("Fade", cgo.transform, Color.black);
        flash = NewImage("Flash", cgo.transform, Color.clear);

        // Кинематографичные полосы 2.39:1
        barTop = NewImage("Bar Top", cgo.transform, Color.black).rectTransform;
        barTop.anchorMin = new Vector2(0, 1); barTop.anchorMax = Vector2.one; barTop.pivot = new Vector2(0.5f, 1); barTop.sizeDelta = new Vector2(0, 138);
        barBottom = NewImage("Bar Bottom", cgo.transform, Color.black).rectTransform;
        barBottom.anchorMin = Vector2.zero; barBottom.anchorMax = new Vector2(1, 0); barBottom.pivot = new Vector2(0.5f, 0); barBottom.sizeDelta = new Vector2(0, 138);

        card = NewText("Card", cgo.transform, textFont, textMaterial, 64, "", new Color(0.88f, 0.82f, 0.74f), Vector2.zero, new Vector2(1600, 300));
        card.alpha = 0f;
        small = NewText("Small", cgo.transform, textFont, textMaterial, 44, "", new Color(0.8f, 0.74f, 0.68f), new Vector2(0, -170), new Vector2(1400, 200));
        small.alpha = 0f;
    }

    static Image NewImage(string name, Transform parent, Color c)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false); Stretch(img.rectTransform); img.color = c; img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, Material mat, float size, string text, Color color, Vector2 pos, Vector2 box)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        if (font != null) t.font = font; if (mat != null) t.fontSharedMaterial = mat;
        t.fontSize = size; t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.rectTransform.sizeDelta = box; t.rectTransform.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    // ---------- Синтез звука ----------

    // Низкий тревожный дрон: две расстроенные квинты, шум ветра, медленная пульсация
    static AudioClip MakeDrone()
    {
        const int rate = 44100; int n = rate * 12;
        var d = new float[n]; var rnd = new System.Random(2);
        float lp = 0f, p1 = 0f, p2 = 0f, p3 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            p1 += 55f / rate; p2 += 55.6f / rate; p3 += 82.2f / rate;
            float tone = Mathf.Sin(p1 * 6.2832f) + Mathf.Sin(p2 * 6.2832f) * 0.8f + Mathf.Sin(p3 * 6.2832f) * 0.35f;
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.01f;
            float swell = 0.75f + 0.25f * Mathf.Sin(t * 6.2832f / 6f);
            d[i] = (tone * 0.18f + lp * 3f) * swell;
        }
        int f = rate / 2; for (int i = 0; i < f; i++) { float w = i / (float)f; d[i] = Mathf.Lerp(d[n - f + i], d[i], w); }
        Normalize(d, 0.7f);
        var c = AudioClip.Create("Trailer Drone", n - f, 1, rate, false);
        var trimmed = new float[n - f]; System.Array.Copy(d, 0, trimmed, 0, n - f); c.SetData(trimmed, 0);
        return c;
    }

    static AudioClip MakeHit()
    {
        const int rate = 44100; int n = rate * 2; var d = new float[n]; var rnd = new System.Random(5); float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; float w = (float)rnd.NextDouble() * 2f - 1f;
            ph += Mathf.Lerp(90f, 40f, Mathf.Clamp01(t / 0.4f)) / rate; lp += (w - lp) * 0.3f;
            d[i] = Mathf.Sin(ph * 6.2832f) * Mathf.Exp(-t * 4f) + lp * Mathf.Exp(-t * 18f) * 0.8f;
        }
        Normalize(d, 0.9f);
        var c = AudioClip.Create("Trailer Hit", n, 1, rate, false); c.SetData(d, 0); return c;
    }

    static AudioClip MakeBoom()
    {
        const int rate = 44100; int n = rate * 5; var d = new float[n]; var rnd = new System.Random(8); float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; float w = (float)rnd.NextDouble() * 2f - 1f;
            ph += Mathf.Lerp(70f, 26f, Mathf.Clamp01(t / 1.5f)) / rate; lp += (w - lp) * Mathf.Lerp(0.4f, 0.02f, Mathf.Clamp01(t));
            d[i] = Mathf.Sin(ph * 6.2832f) * Mathf.Exp(-t * 1.1f) + lp * Mathf.Exp(-t * 2f) * 1.2f;
        }
        Normalize(d, 0.95f);
        var c = AudioClip.Create("Trailer Boom", n, 1, rate, false); c.SetData(d, 0); return c;
    }

    static AudioClip MakeHiss()
    {
        const int rate = 22050; int n = rate * 6; var d = new float[n]; var rnd = new System.Random(3); float lp = 0f;
        for (int i = 0; i < n; i++) { lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.6f; d[i] = lp * 0.4f; }
        var c = AudioClip.Create("Trailer Hiss", n, 1, rate, false); c.SetData(d, 0); return c;
    }

    static void Normalize(float[] d, float to)
    {
        float p = 0f; foreach (float v in d) p = Mathf.Max(p, Mathf.Abs(v));
        if (p > 0f) for (int i = 0; i < d.Length; i++) d[i] *= to / p;
    }
}
