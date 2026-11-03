using UnityEngine;
using UnityEngine.AI;

// Звуки бабки, как в психологических хоррорах: по ним игрок понимает, где она, ещё до того, как увидит.
// - шаркающие шаги в такт ходьбе (в погоне - чаще и громче);
// - тихое хриплое дыхание всё время, пока она рядом;
// - редкие звуки "сама с собой": стон, бормотание, низкое рычание - раз в 10-25 секунд;
// - короткий рык, когда она замечает игрока.
// С другого этажа всё звучит глухо (как сквозь перекрытие). Звуки собираются в коде.
[RequireComponent(typeof(NavMeshAgent))]
public class GrannySounds : MonoBehaviour
{
    public AudioClip growl;            // рычание (берётся из проекта), играется ниже и тише
    public float hearDistance = 20f;
    [Range(0, 1)] public float stepVolume = 0.55f;
    [Range(0, 1)] public float breathVolume = 0.35f;
    [Range(0, 1)] public float idleVolume = 0.5f;
    public Vector2 idleInterval = new Vector2(10f, 25f);

    private NavMeshAgent agent;
    private GhostChase chase;
    private AudioSource steps, breath, voice;
    private AudioLowPassFilter lowPass;
    private AudioClip[] stepClips;
    private AudioClip[] idleClips;
    private float stepTimer;
    private float nextIdle;
    private bool wasChasing;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chase = GetComponent<GhostChase>();
        steps = NewSource(false);
        voice = NewSource(false);
        breath = NewSource(true);
        breath.clip = MakeBreath();
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();

        stepClips = new AudioClip[4];
        for (int i = 0; i < stepClips.Length; i++) stepClips[i] = MakeStep(i);
        idleClips = new[] { MakeMoan(1), MakeMoan(2), MakeMumble(3), MakeMumble(4) };
        nextIdle = Time.time + Random.Range(idleInterval.x, idleInterval.y);
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = hearDistance;
        s.dopplerLevel = 0f;
        return s;
    }

    void OnEnable() { if (breath != null) { breath.time = Random.Range(0f, 4f); breath.Play(); } }
    void OnDisable() { if (breath != null) breath.Stop(); }

    void Update()
    {
        // С другого этажа - глухо и тише
        var cam = Camera.main;
        float dy = cam != null ? Mathf.Abs(cam.transform.position.y - 1.6f - transform.position.y) : 0f;
        float otherFloor = Mathf.InverseLerp(1.8f, 3.2f, dy);
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(500f), otherFloor));
        float floorGain = Mathf.Lerp(1f, 0.3f, otherFloor);

        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        bool chasing = chase != null && agent != null && agent.speed > chase.walkSpeed + 0.1f && speed > 0.5f;

        breath.volume = breathVolume * floorGain * (chasing ? 1.4f : 1f);
        breath.pitch = chasing ? 1.25f : 1f;

        // Шаги: шарканье в такт скорости
        if (speed > 0.15f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                steps.pitch = Random.Range(0.88f, 1.05f);
                steps.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)], stepVolume * floorGain * (chasing ? 1.3f : 1f));
                stepTimer = chasing ? 0.36f : Mathf.Lerp(0.95f, 0.6f, Mathf.InverseLerp(0.5f, 2f, speed));
            }
        }
        else stepTimer = 0.2f;

        // Заметила игрока - короткий рык
        if (chasing && !wasChasing && growl != null && !voice.isPlaying)
        {
            voice.pitch = Random.Range(0.75f, 0.85f);
            voice.PlayOneShot(growl, idleVolume * 1.2f * floorGain);
            nextIdle = Time.time + Random.Range(idleInterval.x, idleInterval.y);
        }
        wasChasing = chasing;

        // Редкие звуки "сама с собой"
        if (Time.time >= nextIdle && !voice.isPlaying)
        {
            nextIdle = Time.time + Random.Range(idleInterval.x, idleInterval.y) * (chasing ? 0.6f : 1f);
            bool useGrowl = growl != null && Random.value < 0.2f;
            voice.pitch = useGrowl ? Random.Range(0.6f, 0.72f) : Random.Range(0.9f, 1.1f);
            voice.PlayOneShot(useGrowl ? growl : idleClips[Random.Range(0, idleClips.Length)], idleVolume * floorGain * (useGrowl ? 0.6f : 1f));
        }
    }

    // В неё попали бутылкой - визгливый вскрик
    public void Hurt()
    {
        if (voice == null) return;
        voice.Stop();
        voice.pitch = 1.5f;
        voice.PlayOneShot(growl != null && Random.value < 0.5f ? growl : idleClips[0], idleVolume * 1.4f);
        nextIdle = Time.time + Random.Range(idleInterval.x, idleInterval.y);
    }

    // ---------- Синтез ----------

    static float Rand(System.Random r) => (float)r.NextDouble() * 2f - 1f;

    // Шарканье: волочение стопы по полу + мягкий удар пятки
    public static AudioClip MakeStep(int seed)
    {
        const int rate = 22050;
        var rnd = new System.Random(100 + seed);
        float drag = 0.18f + (float)rnd.NextDouble() * 0.12f;
        int n = (int)(rate * (drag + 0.25f));
        var d = new float[n];
        float lp = 0f, lp2 = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = Rand(rnd);
            lp += (w - lp) * 0.25f;
            lp2 += (lp - lp2) * 0.3f;
            float scrape = t < drag ? (lp - lp2) * Mathf.Sin(t / drag * Mathf.PI) * 0.6f : 0f;
            float th = t - drag;
            ph += 70f / rate;
            float thud = th > 0f ? (Mathf.Sin(ph * 2f * Mathf.PI) * 0.8f + lp * 0.6f) * Mathf.Exp(-th * 28f) : 0f;
            d[i] = scrape + thud;
        }
        return Finish("Granny Step", d, rate, 0.8f);
    }

    // Медленное хриплое дыхание с бульканьем (луп)
    public static AudioClip MakeBreath()
    {
        const int rate = 22050;
        int n = rate * 6;
        var d = new float[n];
        var rnd = new System.Random(7);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float cyc = Mathf.Repeat(t, 2f) / 2f;                // вдох-выдох за 2 секунды
            float env = cyc < 0.4f ? Mathf.Sin(cyc / 0.4f * Mathf.PI) : Mathf.Sin((cyc - 0.4f) / 0.6f * Mathf.PI) * 0.8f;
            float w = Rand(rnd);
            lp += (w - lp) * (cyc < 0.4f ? 0.35f : 0.18f);
            hp += (lp - hp) * 0.05f;
            float rattle = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 27f) * Mathf.Sin(t * 2f * Mathf.PI * 3f); // хрип
            d[i] = (lp - hp) * env * rattle;
        }
        int fade = rate / 5;
        for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] *= k; d[n - 1 - i] *= k; }
        return Finish("Granny Breath", d, rate, 0.7f);
    }

    // Низкий старушечий стон: голос с дрожью, высота сползает вниз
    public static AudioClip MakeMoan(int seed)
    {
        const int rate = 22050;
        var rnd = new System.Random(200 + seed);
        float len = 1.4f + (float)rnd.NextDouble();
        int n = (int)(rate * len);
        var d = new float[n];
        float ph = 0f, r1 = 0f, r2 = 0f, r3 = 0f, r4 = 0f;
        float f0 = 150f + (float)rnd.NextDouble() * 40f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate, u = t / len;
            float f = f0 * Mathf.Lerp(1.05f, 0.8f, u) * (1f + 0.04f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f));
            ph = Mathf.Repeat(ph + f / rate, 1f);
            float g = ph * 2f - 1f;   // пила - "голосовые связки"
            float a = Resonator(g, 650f, rate, 0.985f, ref r1, ref r2);   // "о-у"
            float b = Resonator(g, 1000f, rate, 0.98f, ref r3, ref r4);
            float env = Mathf.Clamp01(u * 5f) * Mathf.Clamp01((1f - u) * 3f);
            d[i] = (a * 0.8f + b * 0.4f + Rand(rnd) * 0.15f) * env;
        }
        return Finish("Granny Moan", d, rate, 0.7f);
    }

    // Невнятное бормотание: короткие слоги с разной высотой и паузами
    static AudioClip MakeMumble(int seed)
    {
        const int rate = 22050;
        var rnd = new System.Random(300 + seed);
        int syll = 5 + rnd.Next(5);
        var list = new System.Collections.Generic.List<float>();
        float ph = 0f, r1 = 0f, r2 = 0f, r3 = 0f, r4 = 0f;
        for (int s = 0; s < syll; s++)
        {
            float len = 0.09f + (float)rnd.NextDouble() * 0.16f;
            float f0 = 170f + (float)rnd.NextDouble() * 70f;
            float form = 500f + (float)rnd.NextDouble() * 700f;
            int n = (int)(rate * len);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                ph = Mathf.Repeat(ph + f0 * (1f - 0.1f * u) / rate, 1f);
                float g = ph * 2f - 1f;
                float a = Resonator(g, form, rate, 0.98f, ref r1, ref r2);
                float b = Resonator(g, form * 2.3f, rate, 0.97f, ref r3, ref r4);
                list.Add((a + b * 0.4f + Rand(rnd) * 0.2f) * Mathf.Sin(u * Mathf.PI));
            }
            int gap = (int)(rate * (0.03f + (float)rnd.NextDouble() * 0.15f));
            for (int i = 0; i < gap; i++) list.Add(0f);
        }
        return Finish("Granny Mumble", list.ToArray(), rate, 0.6f);
    }

    static float Resonator(float input, float freq, int rate, float r, ref float y1, ref float y2)
    {
        float c = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / rate);
        float y = (1f - r) * input + c * y1 - r * r * y2;
        y2 = y1; y1 = y;
        return y;
    }

    static AudioClip Finish(string name, float[] d, int rate, float peakTo)
    {
        float peak = 0f;
        foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0f) for (int i = 0; i < d.Length; i++) d[i] *= peakTo / peak;
        var c = AudioClip.Create(name, d.Length, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
