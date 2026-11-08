using UnityEngine;

// Уличный звук (генератор, качели): когда слушатель заходит в здание, звук глохнет,
// как будто его слышно сквозь стены - тише и без высоких частот. На улице звучит как обычно.
// На музыке (без фильтра, insideCutoff = 22000) - просто приглушает её в здании, чтобы были слышны
// тихие звуки: шёпот, крысы, сектант.
[RequireComponent(typeof(AudioSource))]
public class OutdoorSound : MonoBehaviour
{
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));
    [Range(0, 1)] public float insideVolume = 0.2f;
    public float insideCutoff = 600f;
    public float fadeSpeed = 2f;

    // Концовка: уличные звуки (генератор, качели) затихают совсем. Музыку (insideCutoff = 22000) не трогаем.
    public static bool EndingSilence;

    // Тишина перед событием: музыка и уличный фон пропадают на время. Шаги и дыхание не трогаем
    private static float hushUntil;
    public static void HushFor(float seconds) => hushUntil = Mathf.Max(hushUntil, Time.time + seconds);
    private static float hush;
    private static int hushFrame = -1;
    // Насколько сейчас приглушён фон: 0 - обычно, 1 - тишина. Им же глушатся лампы
    public static float Hush => hush;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float baseVolume;
    private float inside;
    private float silence;

    void Awake()
    {
        EndingSilence = false;
        hushUntil = 0f;
        hush = 0f;
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
    }

    void Update()
    {
        // В катсцене основная камера выключена - берём того слушателя, что сейчас работает
        var cam = Camera.main;
        Transform listener = cam != null ? cam.transform : null;
        if (listener == null)
        {
            var l = FindAnyObjectByType<AudioListener>();
            if (l != null) listener = l.transform;
        }
        bool listenerInside = listener != null && buildingZone.Contains(listener.position);
        inside = Mathf.MoveTowards(inside, listenerInside ? 1f : 0f, Time.deltaTime * fadeSpeed);

        if (EndingSilence && insideCutoff < 20000f)
            silence = Mathf.MoveTowards(silence, 1f, Time.unscaledDeltaTime * 0.7f);
        if (hushFrame != Time.frameCount)
        {
            hushFrame = Time.frameCount;
            bool hushed = Time.time < hushUntil;
            hush = Mathf.MoveTowards(hush, hushed ? 1f : 0f, Time.deltaTime * (hushed ? 3.5f : 0.7f));
        }
        source.volume = baseVolume * Mathf.Lerp(1f, insideVolume, inside) * (1f - silence) * (1f - hush);
        // Частота среза меняется по экспоненте, чтобы переход на слух был ровным
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(insideCutoff), inside));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireCube(buildingZone.center, buildingZone.size);
    }
}
