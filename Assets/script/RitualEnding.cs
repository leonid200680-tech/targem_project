using System.Collections;
using UnityEngine;

// Финал ритуала (катсцена):
// 1) пентаграмма просыпается, бабку трясёт и она наливается красным;
// 2) воронка дыма затягивает бабку в центр пентаграммы, она крутится и уходит в пол;
// 3) вспышка, удар, клубы дыма;
// 4) из дыма поднимается Анджела, свечи гаснут, камера медленно подъезжает;
// 5) затемнение и экран "Игра пройдена".
public class RitualEnding : MonoBehaviour
{
    [Header("Участники")]
    public Pentagram pentagram;
    public GameObject angela;
    public Transform center;              // центр пентаграммы (на полу)

    [Header("Эффекты")]
    public Material smokeMaterial;
    public Light glowLight;               // красное свечение из центра
    public Light angelaLight;             // мягкий свет на Анджелу

    [Header("Тайминги (сек)")]
    public float awakenTime = 2f;
    public float suckTime = 3.2f;
    public float riseTime = 3.5f;
    public float holdOnAngela = 4f;

    [Header("Экран конца")]
    public GameCompleteScreen completeScreen;

    public bool IsPlaying { get; private set; }

    private ParticleSystem vortex, burst, haze;
    private AudioSource rumble;
    private Transform cam;
    private Vector3 camBasePos;
    private Quaternion camBaseRot;
    private float shake;

    void Awake()
    {
        if (angela != null)
        {
            // В FBX из Character Creator лежит фон-задник: прячем его, даже если переимпорт сбросил настройку
            foreach (var t in angela.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Backdrop")) t.gameObject.SetActive(false);
            angela.SetActive(false);
        }
        if (glowLight != null) glowLight.enabled = false;
        if (angelaLight != null) angelaLight.enabled = false;
        // Свет в тумане не должен висеть шаром: подсветка Анджелы не светится в нём вовсе, красное свечение - слабо
        SetVolumetric(angelaLight, 0f);
        SetVolumetric(glowLight, 0.3f);
    }

    public IEnumerator Play()
    {
        IsPlaying = true;
        OutdoorSound.EndingSilence = true;
        var p = pentagram;

        // Игрок больше не управляет, пауза недоступна
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;
        var pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null) pause.enabled = false;
        if (p.handsToHide != null) p.handsToHide.SetActive(false);
        if (p.hudToHide != null) p.hudToHide.SetActive(false);

        Camera mainCam = Camera.main;
        if (p.cutsceneCamera != null)
        {
            if (mainCam != null) mainCam.gameObject.SetActive(false);
            p.cutsceneCamera.SetActive(true);
            cam = p.cutsceneCamera.transform;
        }
        else cam = mainCam != null ? mainCam.transform : null;
        if (cam != null) { camBasePos = cam.position; camBaseRot = cam.rotation; }

        var realGhost = FindAnyObjectByType<GhostChase>();
        if (realGhost != null) realGhost.gameObject.SetActive(false);

        GameObject granny = p.ritualGhost;
        if (granny != null) granny.SetActive(true);
        var grannyAnim = granny != null ? granny.GetComponent<Animator>() : null;

        if (p.exorcismSound != null) AudioSource.PlayClipAtPoint(p.exorcismSound, center.position, 1f);
        BuildFx();
        rumble.Play();

        Vector3 c = center.position;

        // ---------- 1. Пробуждение ----------
        var mats = new System.Collections.Generic.List<Material>();
        if (granny != null)
            foreach (var r in granny.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) { m.EnableKeyword("_EMISSION"); mats.Add(m); }
        Vector3 gStart = granny != null ? granny.transform.position : c;
        Vector3 gScale = granny != null ? granny.transform.localScale : Vector3.one;
        Quaternion gRot = granny != null ? granny.transform.rotation : Quaternion.identity;

        glowLight.enabled = true;
        vortex.Play();
        StartCoroutine(FadeTint(vortex, new Color(0.3f, 0.07f, 0.06f, 0.7f), awakenTime, suckTime, 1.2f));
        float t = 0f;
        while (t < awakenTime)
        {
            t += Time.deltaTime;
            float k = t / awakenTime;
            SetGlow(k * 0.5f);
            SetEmission(mats, p.burnColor * (k * k * 4f));
            if (granny != null) granny.transform.position = gStart + Random.insideUnitSphere * 0.03f * k;
            shake = 0.01f * k;
            rumble.volume = Mathf.Lerp(0.2f, 0.6f, k);
            yield return null;
        }

        // ---------- 2. Затягивание в пентаграмму ----------
        if (grannyAnim != null) grannyAnim.speed = 3f;
        t = 0f;
        float spin = 0f;
        while (t < suckTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / suckTime);
            float e = k * k * k;   // сначала медленно, потом рывком вниз
            spin += Time.deltaTime * Mathf.Lerp(90f, 1600f, k * k);

            if (granny != null)
            {
                Vector3 toCenter = Vector3.Lerp(gStart, new Vector3(c.x, gStart.y, c.z), Mathf.SmoothStep(0f, 1f, k));
                granny.transform.position = toCenter + Vector3.down * (2.4f * e) + Random.insideUnitSphere * 0.04f * (1f - k);
                granny.transform.rotation = Quaternion.AngleAxis(spin, Vector3.up) * gRot;
                float thin = Mathf.Lerp(1f, 0.15f, e);
                float tall = Mathf.Lerp(1f, 1.5f, Mathf.Sin(k * Mathf.PI * 0.5f)) * Mathf.Lerp(1f, 0.4f, e);
                granny.transform.localScale = new Vector3(gScale.x * thin, gScale.y * tall, gScale.z * thin);
            }
            SetEmission(mats, p.burnColor * Mathf.Lerp(4f, 10f, k));
            SetGlow(Mathf.Lerp(0.5f, 1f, k) * (0.8f + 0.2f * Mathf.Sin(Time.time * 40f)));
            var vm = vortex.main; vm.simulationSpeed = Mathf.Lerp(1f, 3f, k);
            shake = Mathf.Lerp(0.015f, 0.06f, k);
            rumble.volume = Mathf.Lerp(0.6f, 1f, k);
            rumble.pitch = Mathf.Lerp(1f, 1.6f, k);
            yield return null;
        }

        // ---------- 3. Вспышка и дым ----------
        if (granny != null) granny.SetActive(false);
        vortex.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        burst.Play();
        burst.Emit(70); // одноразовый выброс надёжнее, чем Burst в настройках эмиссии
        haze.Play();
        StartCoroutine(FadeTint(burst, new Color(0.36f, 0.34f, 0.33f, 0.8f), 0.15f, 1.5f, 3.5f));
        StartCoroutine(FadeTint(haze, new Color(0.42f, 0.4f, 0.39f, 0.55f), 0.8f, 3.5f, 4f));
        rumble.pitch = 0.6f;
        AudioSource.PlayClipAtPoint(MakeBoom(), center.position, 1f);
        if (p.flashLight != null)
        {
            p.flashLight.transform.position = c + Vector3.up * 1.2f;
            p.flashLight.enabled = true;
        }
        shake = 0.18f;
        float flash = 0f;
        while (flash < 0.6f)
        {
            flash += Time.deltaTime;
            float k = flash / 0.6f;
            SetGlow(Mathf.Lerp(3f, 0.25f, k));
            if (p.flashLight != null) p.flashLight.intensity = Mathf.Lerp(p.flashLight.intensity, 0f, k * 0.2f);
            shake = Mathf.Lerp(0.18f, 0.02f, k);
            yield return null;
        }
        if (p.flashLight != null) p.flashLight.enabled = false;

        // ---------- 4. Анджела поднимается из дыма ----------
        if (angela != null)
        {
            angela.SetActive(true);
            Vector3 aEnd = new Vector3(c.x, c.y, c.z);
            Vector3 aStart = aEnd + Vector3.down * 2f;
            angela.transform.position = aStart;
            if (cam != null)
            {
                Vector3 look = cam.position - aEnd; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) angela.transform.rotation = Quaternion.LookRotation(look);
            }
            angelaLight.enabled = true;
            t = 0f;
            while (t < riseTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / riseTime);
                angela.transform.position = Vector3.Lerp(aStart, aEnd, 1f - Mathf.Pow(1f - k, 3f));
                angelaLight.intensity = Mathf.Lerp(0f, 900f, k);
                SetGlow(Mathf.Lerp(0.25f, 0.08f, k));
                rumble.volume = Mathf.Lerp(1f, 0.25f, k);
                shake = Mathf.Lerp(0.02f, 0.004f, k);
                yield return null;
            }
        }

        // Свечи гаснут одна за другой
        StartCoroutine(BlowOutCandles());

        // ---------- 5. Камера подъезжает к Анджеле ----------
        if (cam != null && angela != null)
        {
            Vector3 face = angela.transform.position + Vector3.up * 1.55f;
            Vector3 from = cam.position;
            Vector3 to = face + (from - face).normalized * 1.3f;
            Quaternion rFrom = cam.rotation, rTo = Quaternion.LookRotation(face - to);
            t = 0f;
            while (t < holdOnAngela)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / holdOnAngela));
                camBasePos = Vector3.Lerp(from, to, k);
                camBaseRot = Quaternion.Slerp(rFrom, rTo, k);
                rumble.volume = Mathf.Lerp(0.25f, 0f, k);
                shake = 0.003f;
                yield return null;
            }
        }
        rumble.Stop();

        // Игрок терял рассудок - вернулась не только Анджела: на мгновение вместо неё стоит бабка
        if (Sanity.Lost > 0 && angela != null) yield return NotAlone();

        // ---------- Конец ----------
        if (QuestManager.Instance != null) QuestManager.Instance.OnRitualComplete();
        if (completeScreen == null) completeScreen = FindAnyObjectByType<GameCompleteScreen>(FindObjectsInactive.Include);
        if (completeScreen != null)
        {
            Sanity.ApplyEnding(completeScreen, false);
            completeScreen.Show();
        }
        IsPlaying = false;
    }

    // Свет мигает, и в темноте на месте Анджелы дважды мелькает бабка с глухим ударом
    IEnumerator NotAlone()
    {
        GameObject granny = null;
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        if (hm != null && hm.realGhost != null)
        {
            hm.realGhost.SetActive(false);
            granny = Instantiate(hm.realGhost);
            foreach (var b in granny.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
            foreach (var a in granny.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Destroy(a);
            granny.transform.SetPositionAndRotation(angela.transform.position, angela.transform.rotation);
        }
        AudioClip boom = MakeBoom();
        yield return new WaitForSeconds(0.8f);
        for (int i = 0; i < 2; i++)
        {
            angelaLight.enabled = false;
            yield return new WaitForSeconds(0.12f);
            angela.SetActive(false);
            if (granny != null) granny.SetActive(true);
            angelaLight.enabled = true;
            AudioSource.PlayClipAtPoint(boom, center.position, 0.7f);
            shake = 0.05f;
            yield return new WaitForSeconds(i == 0 ? 0.15f : 0.35f);
            angelaLight.enabled = false;
            if (granny != null) granny.SetActive(false);
            angela.SetActive(true);
            yield return new WaitForSeconds(0.1f);
            angelaLight.enabled = true;
            shake = 0.003f;
            yield return new WaitForSeconds(0.9f);
        }
        if (granny != null) Destroy(granny);
    }

    IEnumerator BlowOutCandles()
    {
        foreach (var candle in FindObjectsByType<CandleInteract>(FindObjectsSortMode.None))
        {
            if (candle.fireEffect != null) candle.fireEffect.SetActive(false);
            yield return new WaitForSeconds(Random.Range(0.25f, 0.6f));
        }
    }

    void LateUpdate()
    {
        if (!IsPlaying || cam == null) return;
        // Плавная тряска по шуму: случайный рывок каждый кадр размазывается motion blur в кашу
        float t = Time.time * 9f;
        Vector3 n = new Vector3(Mathf.PerlinNoise(t, 0.1f), Mathf.PerlinNoise(0.7f, t), Mathf.PerlinNoise(t, 3.3f)) * 2f - Vector3.one;
        cam.position = camBasePos + n * shake;
        cam.rotation = camBaseRot * Quaternion.Euler(n.y * shake * 12f, n.x * shake * 12f, 0f);
    }

    static void SetVolumetric(Light light, float dimmer)
    {
        if (light != null && light.TryGetComponent(out UnityEngine.Rendering.HighDefinition.HDAdditionalLightData hd))
            hd.volumetricDimmer = dimmer;
    }

    void SetGlow(float k)
    {
        if (glowLight == null) return;
        glowLight.intensity = 2500f * k;
    }

    static void SetEmission(System.Collections.Generic.List<Material> mats, Color c)
    {
        foreach (var m in mats) m.SetColor("_EmissionColor", c);
    }

    // ---------- Дым ----------

    void BuildFx()
    {
        if (vortex != null) return;
        Vector3 c = center.position;

        // Воронка: тёмный дым кружит и затягивается к центру и вниз
        vortex = NewSystem("Vortex", c + Vector3.up * 0.1f);
        var m = vortex.main;
        m.startLifetime = 2.2f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        m.startColor = new Color(0.22f, 0.07f, 0.06f, 0.8f); m.maxParticles = 400;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var em = vortex.emission; em.rateOverTime = 90f;
        var sh = vortex.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.7f; sh.radiusThickness = 0.2f;
        var vel = vortex.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(3.5f); vel.radial = new ParticleSystem.MinMaxCurve(-0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f);
        vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f);
        Fade(vortex, 0.7f);
        var rot = vortex.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        // Взрыв дыма после вспышки
        burst = NewSystem("Smoke Burst", c + Vector3.up * 0.2f);
        m = burst.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f); m.startColor = new Color(0.33f, 0.31f, 0.3f, 0.85f);
        m.gravityModifier = -0.04f; m.maxParticles = 200; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        em = burst.emission; em.rateOverTime = 0f;
        sh = burst.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.8f;
        var limit = burst.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0.3f; limit.dampen = 0.15f;
        Fade(burst, 0.85f);
        rot = burst.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        // Стелющаяся дымка у пола, из которой выходит Анджела
        haze = NewSystem("Floor Haze", c + Vector3.up * 0.05f);
        m = haze.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        m.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.6f); m.startColor = new Color(0.4f, 0.38f, 0.37f, 0.5f);
        m.duration = 6f; m.loop = false; m.maxParticles = 150; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        em = haze.emission; em.rateOverTime = 14f;
        sh = haze.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.4f;
        Fade(haze, 0.5f);

        // Низкий гул под всей сценой
        rumble = gameObject.AddComponent<AudioSource>();
        rumble.clip = MakeRumble();
        rumble.loop = true; rumble.spatialBlend = 0f; rumble.volume = 0.2f; rumble.playOnAwake = false;
    }

    ParticleSystem NewSystem(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // локальная Z смотрит вверх: круги лежат на полу, воронка крутится вокруг вертикали
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = new Material(smokeMaterial);       // у каждого облака своя тонировка
        r.material.SetColor("_UnlitColor", new Color(1f, 1f, 1f, 0f));
        r.sortMode = ParticleSystemSortMode.Distance;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    // HDRP/Unlit не учитывает цвет отдельной частицы, поэтому прозрачность всего облака
    // ведём через цвет его материала; отдельные частицы гаснут, уменьшаясь в конце жизни
    static void Fade(ParticleSystem ps, float peak)
    {
        var sol = ps.sizeOverLifetime;
        if (!sol.enabled)
        {
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(0.8f, 1.2f), new Keyframe(1f, 0f)));
        }
    }

    IEnumerator FadeTint(ParticleSystem ps, Color color, float fadeIn, float hold, float fadeOut)
    {
        var mat = ps.GetComponent<ParticleSystemRenderer>().material;
        float t = 0f, total = fadeIn + hold + fadeOut;
        while (t < total)
        {
            t += Time.deltaTime;
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            mat.SetColor("_UnlitColor", new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(a)));
            yield return null;
        }
        mat.SetColor("_UnlitColor", new Color(color.r, color.g, color.b, 0f));
    }

    // ---------- Звук ----------

    static AudioClip MakeRumble()
    {
        const int rate = 22050;
        int n = rate * 4;
        var d = new float[n];
        var rnd = new System.Random(4);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.02f;
            lp2 += (lp - lp2) * 0.05f;
            float drone = Mathf.Sin(t * 2f * Mathf.PI * 38f) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * 57f) * 0.15f;
            d[i] = lp2 * 6f + drone * (0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 0.5f));
        }
        int fade = rate / 4;
        for (int i = 0; i < fade; i++) { float w = i / (float)fade; d[i] *= w; d[n - 1 - i] *= w; }
        Normalize(d, 0.8f);
        var c = AudioClip.Create("Ritual Rumble", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeBoom()
    {
        const int rate = 44100;
        int n = rate * 4;
        var d = new float[n];
        var rnd = new System.Random(8);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            ph += Mathf.Lerp(80f, 28f, Mathf.Clamp01(t / 1.2f)) / rate;
            lp += (white - lp) * Mathf.Lerp(0.5f, 0.02f, Mathf.Clamp01(t / 0.8f));
            d[i] = Mathf.Sin(ph * 2f * Mathf.PI) * Mathf.Exp(-t * 1.4f) + lp * Mathf.Exp(-t * 2.5f) * 1.5f;
        }
        Normalize(d, 0.95f);
        var c = AudioClip.Create("Ritual Boom", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    static void Normalize(float[] d, float to)
    {
        float peak = 0f;
        foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0f) for (int i = 0; i < d.Length; i++) d[i] *= to / peak;
    }
}
