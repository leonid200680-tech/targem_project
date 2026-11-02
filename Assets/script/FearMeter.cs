using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Шкала страха: пока игрок внутри здания, страх медленно копится (в темноте и рядом с бабкой - быстрее).
// Чем выше страх, тем чаще моргают лампы, сильнее темнеют края экрана и изредка слышатся голоса:
// перевёрнутый шёпот, приглушённая речь за стеной, шаги где-то рядом. Голоса звучат редко и тихо,
// со случайными паузами в минуты, никогда во время диалогов и сцен, и не повторяются подряд.
// Выполненный квест немного успокаивает, но с каждым шагом сюжета "пол" страха поднимается.
public class FearMeter : MonoBehaviour
{
    public static FearMeter Instance { get; private set; }
    // Текущий страх 0..1 (0, если шкалы нет в сцене)
    public static float Level => Instance != null ? Instance.level : 0f;

    [Header("Рост страха")]
    [Tooltip("За сколько секунд внутри здания страх дорастает от 0 до максимума (при свете)")]
    public float riseTime = 540f;
    [Tooltip("Во сколько раз быстрее растёт страх, когда фонарик выключен")]
    public float darkMultiplier = 1.7f;
    [Tooltip("Прибавка в секунду, когда бабка рядом")]
    public float ghostRise = 0.04f;
    public float ghostDistance = 15f;
    [Tooltip("Спад в секунду на улице")]
    public float outsideFall = 0.006f;
    [Tooltip("Насколько успокаивает выполненный квест")]
    public float questRelief = 0.12f;
    [Tooltip("Минимальный страх к концу сюжета: с каждым шагом квеста он поднимается")]
    public float progressFloor = 0.4f;

    [Header("Здание")]
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));

    [Header("Голоса")]
    [Tooltip("Реплики, из которых нарезается шёпот (играются задом наперёд и приглушённо)")]
    public AudioClip[] voiceClips;
    public AudioClip stepsClip;
    [Tooltip("С какого страха начинаются голоса")]
    [Range(0, 1)] public float voicesFrom = 0.25f;
    [Tooltip("Пауза между голосами при низком и при максимальном страхе, сек")]
    public float maxGap = 200f;
    public float minGap = 55f;
    [Range(0, 1)] public float voiceVolume = 0.8f;

    [Header("Экран")]
    [Range(0, 1)] public float maxVignette = 0.4f;
    [Range(0, 1)] public float maxGrain = 0.35f;

    [Header("Отладка")]
    [Range(0, 1)] [SerializeField] private float level;

    private PlayerController player;
    private Flashlight flashlight;
    private HallucinationManager hallucinations;
    private RitualEnding ending;
    private QuestManager.Step lastStep;
    private float nextVoice;
    private int lastKind = -1;
    private int lastClip = -1;

    private AudioSource voice;
    private AudioLowPassFilter lowPass;
    private AudioClip fragment;

    private Volume volume;
    private Vignette vignette;
    private FilmGrain grain;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (volume != null && volume.profile != null) Destroy(volume.profile);
        if (fragment != null) Destroy(fragment);
    }

    void Start()
    {
        ending = FindAnyObjectByType<RitualEnding>(FindObjectsInactive.Include);
        hallucinations = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);

        // Клипы не грузятся заранее: подгружаем, чтобы из них можно было резать фрагменты
        if (voiceClips != null) foreach (var c in voiceClips) if (c != null) c.LoadAudioData();
        if (stepsClip != null) stepsClip.LoadAudioData();

        var go = new GameObject("Fear Voice");
        go.transform.SetParent(transform, false);
        voice = go.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 2f;
        voice.maxDistance = 25f;
        voice.dopplerLevel = 0f;
        lowPass = go.AddComponent<AudioLowPassFilter>();
        var reverb = go.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = AudioReverbPreset.Hallway;

        CreateVolume();
        ScheduleVoice();
    }

    void CreateVolume()
    {
        var go = new GameObject("Fear Volume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 5f;
        volume.weight = 1f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(0.6f);
        vignette.color.Override(Color.black);
        grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0f);
        volume.profile = profile;
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null) flashlight = player.GetComponent<Flashlight>();
        }

        var qm = QuestManager.Instance;
        bool finished = (qm != null && qm.CurrentStep >= QuestManager.Step.Done) || (ending != null && ending.IsPlaying);
        bool playing = player != null && player.enabled && qm != null && qm.PlayTime > 0f && !finished;

        if (finished)
        {
            level = Mathf.MoveTowards(level, 0f, Time.deltaTime * 0.2f);
            if (voice.isPlaying) voice.volume = Mathf.MoveTowards(voice.volume, 0f, Time.deltaTime);
        }
        else if (playing)
        {
            UpdateLevel(qm);
            UpdateVoices();
        }

        ApplyScreen();
    }

    void UpdateLevel(QuestManager qm)
    {
        // Выполненный шаг квеста - короткое облегчение
        if (qm.CurrentStep != lastStep)
        {
            if (qm.CurrentStep > lastStep) level -= questRelief;
            lastStep = qm.CurrentStep;
        }

        Vector3 pos = player.transform.position;
        bool inside = buildingZone.Contains(pos);
        if (inside)
        {
            bool lightOn = flashlight != null && flashlight.spotLight != null && flashlight.spotLight.enabled;
            level += Time.deltaTime / riseTime * (lightOn ? 1f : darkMultiplier);
        }
        else
        {
            level -= Time.deltaTime * outsideFall;
        }

        var ghost = hallucinations != null ? hallucinations.realGhost : null;
        if (ghost != null && ghost.activeInHierarchy && Vector3.Distance(ghost.transform.position, pos) < ghostDistance)
            level += Time.deltaTime * ghostRise;

        float floor = Mathf.Max(progressFloor * (float)qm.CurrentStep / (float)QuestManager.Step.Done, Sanity.FearFloor);
        level = Mathf.Clamp(level, floor, 1f);
    }

    void ApplyScreen()
    {
        if (vignette == null) return;
        // Плавная кривая: на малом страхе почти незаметно
        float k = level * level;
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, k * maxVignette, Time.deltaTime * 2f);
        grain.intensity.value = Mathf.Lerp(grain.intensity.value, k * maxGrain, Time.deltaTime * 2f);
    }

    // ---------- Голоса ----------

    void ScheduleVoice()
    {
        float gap = Mathf.Lerp(maxGap, minGap, Mathf.InverseLerp(voicesFrom, 1f, level));
        nextVoice = Time.time + gap * Random.Range(0.75f, 1.35f);
    }

    void UpdateVoices()
    {
        if (Time.time < nextVoice || voice.isPlaying) return;

        // Не мешаем сюжету: ни в диалоге, ни на улице, ни когда рядом бабка
        var ghost = hallucinations != null ? hallucinations.realGhost : null;
        bool busy = DialogueManager.IsDialogueActive || PauseMenu.GameIsPaused
            || !buildingZone.Contains(player.transform.position)
            || (ghost != null && ghost.activeInHierarchy);
        if (level < voicesFrom || busy)
        {
            // Проверим снова чуть позже, не сбивая длинную паузу полностью
            nextVoice = Time.time + Random.Range(8f, 15f);
            return;
        }

        PlayVoice();
        ScheduleVoice();
    }

    void PlayVoice()
    {
        // 0 - перевёрнутый шёпот за спиной, 1 - приглушённая речь за стеной, 2 - шаги (только на сильном страхе)
        int kind;
        int guard = 0;
        do kind = Random.Range(0, level > 0.5f && stepsClip != null ? 3 : 2);
        while (kind == lastKind && guard++ < 4);
        lastKind = kind;

        AudioClip src;
        if (kind == 2) src = stepsClip;
        else
        {
            if (voiceClips == null || voiceClips.Length == 0) return;
            int i;
            guard = 0;
            do i = Random.Range(0, voiceClips.Length);
            while (i == lastClip && voiceClips.Length > 1 && guard++ < 4);
            lastClip = i;
            src = voiceClips[i];
        }
        if (src == null || src.loadState != AudioDataLoadState.Loaded) return;

        var clip = kind == 2 ? MakeSteps(src, Random.Range(4, 8)) : MakeFragment(src, Random.Range(1.4f, 2.6f), kind == 0);
        if (clip == null) return;
        if (fragment != null) Destroy(fragment);
        fragment = clip;

        // Откуда звук: шёпот - близко за спиной, речь и шаги - дальше, как из соседней комнаты
        var cam = Camera.main != null ? Camera.main.transform : player.transform;
        Vector3 back = -cam.forward; back.y = 0f; back.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, back) * Random.Range(-0.7f, 0.7f);
        float dist = kind == 0 ? Random.Range(2.5f, 4.5f) : Random.Range(7f, 11f);
        voice.transform.position = cam.position + (back + side).normalized * dist;

        lowPass.cutoffFrequency = kind == 0 ? 2200f : kind == 1 ? 900f : 700f;
        voice.pitch = kind == 0 ? Random.Range(0.72f, 0.85f) : kind == 1 ? Random.Range(0.85f, 0.95f) : 1f;
        voice.volume = voiceVolume * (kind == 0 ? 0.9f : 1f) * Mathf.Lerp(0.75f, 1f, level);
        voice.clip = clip;
        voice.Play();
    }

    // Вырезает из клипа кусок с речью (самый громкий из нескольких попыток), делает моно,
    // при необходимости разворачивает и сглаживает края, чтобы не было щелчков
    static AudioClip MakeFragment(AudioClip src, float seconds, bool reverse)
    {
        int ch = src.channels, rate = src.frequency;
        int len = Mathf.Min(Mathf.RoundToInt(seconds * rate), src.samples);
        if (len <= rate / 4) return null;

        var buf = new float[len * ch];
        float[] best = null;
        float bestRms = -1f;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int start = Random.Range(0, Mathf.Max(1, src.samples - len));
            if (!src.GetData(buf, start)) return null;
            var mono = new float[len];
            double sum = 0;
            for (int i = 0; i < len; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += buf[i * ch + c];
                s /= ch;
                mono[i] = s;
                sum += s * s;
            }
            float rms = (float)System.Math.Sqrt(sum / len);
            if (rms > bestRms) { bestRms = rms; best = mono; }
        }

        if (reverse) System.Array.Reverse(best);

        int fade = Mathf.Min(len / 3, Mathf.RoundToInt(rate * 0.35f));
        for (int i = 0; i < fade; i++)
        {
            float w = Mathf.SmoothStep(0f, 1f, i / (float)fade);
            best[i] *= w;
            best[len - 1 - i] *= w;
        }

        var clip = AudioClip.Create("Fear Voice", len, 1, rate, false);
        clip.SetData(best, 0);
        return clip;
    }

    // Несколько неторопливых шагов из одного звука шага: громкость нарастает и стихает,
    // будто кто-то прошёл в соседней комнате
    static AudioClip MakeSteps(AudioClip src, int count)
    {
        int ch = src.channels, rate = src.frequency;
        var buf = new float[src.samples * ch];
        if (!src.GetData(buf, 0)) return null;

        int interval = Mathf.RoundToInt(rate * 0.62f);
        int len = interval * count + src.samples;
        var data = new float[len];
        for (int s = 0; s < count; s++)
        {
            float u = (s + 0.5f) / count;
            float gain = Mathf.Sin(u * Mathf.PI) * Random.Range(0.75f, 1f);
            int at = s * interval + Random.Range(-rate / 25, rate / 25);
            for (int i = 0; i < src.samples; i++)
            {
                int j = at + i;
                if (j < 0 || j >= len) continue;
                float v = 0f;
                for (int c = 0; c < ch; c++) v += buf[i * ch + c];
                data[j] += v / ch * gain;
            }
        }

        var clip = AudioClip.Create("Fear Steps", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Для проверки из редактора
    public void SetLevel(float value) => level = Mathf.Clamp01(value);
    public void PlayVoiceNow() { PlayVoice(); ScheduleVoice(); }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.1f, 0.1f, 0.4f);
        Gizmos.DrawWireCube(buildingZone.center, buildingZone.size);
    }
}
