using UnityEngine;

// Звуки перепуганного наркомана, по которым игрок находит его на 2 этаже:
// сбивчивое дрожащее дыхание и всхлипы (генерируются в коде) и бормотание его же
// фразами из озвучки ("тише, тише...") с дрожащим голосом.
// Слышно только внутри здания; после первой встречи - чуть тише, до финала.
public class ScaredNPCSounds : MonoBehaviour
{
    public NPCDialogueController npc;

    [Tooltip("С какого шага квеста начинает звучать")]
    public QuestManager.Step startStep = QuestManager.Step.ThirdFloor;
    [Tooltip("Громкость после первой встречи (доля от громкости, пока его ищут)")]
    [Range(0, 1)] public float afterMeetLevel = 0.65f;

    [Header("Где слышно")]
    [Tooltip("Звук слышен, только когда слушатель внутри этой зоны (здание)")]
    public Bounds audibleZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));
    public float hearDistance = 22f;
    public float fadeSpeed = 1.5f;
    [Tooltip("Громкость, когда слушатель на другом этаже")]
    [Range(0, 1)] public float throughFloorVolume = 0.2f;
    public float throughFloorCutoff = 450f;

    [Header("Бормотание (его реплики из озвучки)")]
    public AudioClip[] mutterClips;
    public Vector2 mutterPause = new Vector2(4f, 9f);
    public Vector2 mutterPitch = new Vector2(0.94f, 1.03f);
    [Range(0, 1)] public float mutterVolume = 0.75f;
    [Range(0, 1)] public float tremolo = 0.35f;   // дрожь в голосе

    [Header("Дыхание и всхлипы")]
    [Range(0, 1)] public float breathVolume = 0.8f;

    private AudioSource breath;
    private AudioSource voice;
    private AudioLowPassFilter lowPass;
    private float level;      // идёт ли поиск (квест)
    private float inside;     // слушатель в здании
    private float nextMutter;

    void Awake()
    {
        if (npc == null) npc = GetComponentInParent<NPCDialogueController>();
        breath = MakeSource(MakeBreathClip(20f), true);
        voice = MakeSource(null, false);
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
    }

    AudioSource MakeSource(AudioClip clip, bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = hearDistance;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        return s;
    }

    void Update()
    {
        var qm = QuestManager.Instance;
        // Звучит и после первой встречи: к нему ещё возвращаться с книгой и за куклой.
        // Пока его ищут - громко, потом чуть тише; замолкает только к финалу
        bool searching = npc == null || npc.currentState == NPCDialogueController.NPCState.FirstMeet;
        bool present = npc == null || npc.currentState != NPCDialogueController.NPCState.ThirdFloorAmbush;
        bool want = qm != null && qm.CurrentStep >= startStep && qm.CurrentStep < QuestManager.Step.Done
            && present && !DialogueManager.IsDialogueActive;
        float target = want ? (searching ? 1f : afterMeetLevel) : 0f;
        level = Mathf.MoveTowards(level, target, Time.deltaTime * fadeSpeed * 0.4f);

        // На улице не слышно: глушим, когда слушатель (камера игрока) вне здания
        var cam = Camera.main;
        bool listenerInside = cam != null && audibleZone.Contains(cam.transform.position);
        inside = Mathf.MoveTowards(inside, listenerInside ? 1f : 0f, Time.deltaTime * fadeSpeed);

        // Сквозь перекрытие: с другого этажа его слышно тихо и глухо, чтобы было понятно,
        // что он не рядом, а выше или ниже
        float dy = cam != null ? Mathf.Abs(cam.transform.position.y - 1.6f - transform.position.y) : 0f;   // 1.6 - рост до глаз
        float otherFloor = Mathf.InverseLerp(1.8f, 3.2f, dy);
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(throughFloorCutoff), otherFloor));

        float gain = level * inside * Mathf.Lerp(1f, throughFloorVolume, otherFloor);
        breath.volume = gain * breathVolume;
        if (level > 0f && !breath.isPlaying) breath.Play();
        else if (level <= 0f && breath.isPlaying) breath.Stop();

        // Дрожащий голос: быстрые неровные колебания громкости
        float t = Time.time;
        float shake = 0.5f + 0.5f * Mathf.Sin(t * 47f) * Mathf.PerlinNoise(t * 3f, 0.5f);
        voice.volume = gain * mutterVolume * (1f - tremolo * shake);

        if (mutterClips != null && mutterClips.Length > 0 && level > 0.6f && !voice.isPlaying && t >= nextMutter)
        {
            voice.clip = mutterClips[Random.Range(0, mutterClips.Length)];
            voice.pitch = Random.Range(mutterPitch.x, mutterPitch.y);
            voice.Play();
            nextMutter = t + voice.clip.length / voice.pitch + Random.Range(mutterPause.x, mutterPause.y);
        }
        if (level <= 0f && voice.isPlaying) voice.Stop();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
        Gizmos.DrawWireCube(audibleZone.center, audibleZone.size);
    }

    // ---------- Синтез дыхания ----------

    // Частое сбивчивое дыхание напуганного человека: вдохи иногда судорожные (всхлип),
    // на выдохе иногда тихое поскуливание, изредка шмыганье носом.
    static AudioClip MakeBreathClip(float seconds)
    {
        const int rate = 22050;
        int n = Mathf.RoundToInt(rate * seconds);
        var data = new float[n];
        var rnd = new System.Random(13);
        float Rand() => (float)rnd.NextDouble();

        float r1a = 0f, r1b = 0f, r2a = 0f, r2b = 0f; // резонаторы голоса
        float phase = 0f;

        int i = 0;
        while (i < n)
        {
            // Один цикл дыхания: вдох + выдох, быстрый и неровный
            float inhale = 0.35f + Rand() * 0.3f;
            float exhale = 0.45f + Rand() * 0.5f;
            bool whimper = Rand() < 0.75f;       // поскуливание на выдохе
            float whimperStart = 260f + Rand() * 90f;
            float pause = 0.05f + Rand() * 0.25f;

            int inN = (int)(inhale * rate), exN = (int)(exhale * rate), paN = (int)(pause * rate);

            // Вдох беззвучный: шум дыхания игроки слышали как неприятное шуршание
            i += inN;

            // Выдох: только голос ("мм-ы-ы", высота плывёт вниз и дрожит)
            for (int k = 0; k < exN && i < n; k++, i++)
            {
                float u = k / (float)exN;
                float v = 0f;
                if (whimper)
                {
                    float tt = k / (float)rate;
                    float f0 = Mathf.Lerp(whimperStart, whimperStart * 0.68f, u) * (1f + 0.03f * Mathf.Sin(tt * 2f * Mathf.PI * 6.5f) + 0.01f * (Rand() - 0.5f));
                    phase = Mathf.Repeat(phase + f0 / rate, 1f);
                    // Мягкий голосовой импульс (несколько гармоник)
                    float g = Mathf.Sin(phase * 2f * Mathf.PI) + 0.5f * Mathf.Sin(phase * 4f * Mathf.PI) + 0.25f * Mathf.Sin(phase * 6f * Mathf.PI);
                    float f1 = Resonator(g, 320f, rate, 0.985f, ref r1a, ref r1b);   // носовое "м"
                    float f2 = Resonator(g, 1900f, rate, 0.97f, ref r2a, ref r2b);   // "ы"
                    float venv = Mathf.Clamp01(u * 5f) * Mathf.Clamp01((1f - u) * 2.5f);
                    v = (f1 * 0.9f + f2 * 0.35f) * venv * 0.045f;
                }
                data[i] = v;
            }

            i += paN;
        }

        // Нормализуем, чтобы самый громкий всхлип не клипповал
        float peak = 0f;
        foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak > 0f)
            for (int k = 0; k < n; k++) data[k] *= 0.6f / peak;

        // Склейка конца с началом, чтобы луп не щёлкал
        int fade = rate / 5;
        for (int k = 0; k < fade; k++)
        {
            float w = k / (float)fade;
            data[k] *= w;
            data[n - 1 - k] *= w;
        }

        var clip = AudioClip.Create("Scared Breathing", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Resonator(float input, float freq, int rate, float r, ref float y1, ref float y2)
    {
        float c = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / rate);
        float y = (1f - r) * input + c * y1 - r * r * y2;
        y2 = y1;
        y1 = y;
        return y;
    }
}
