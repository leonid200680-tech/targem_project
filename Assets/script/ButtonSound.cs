using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Звук кнопки меню: при наведении - короткий мягкий "тик", при нажатии - глухой щелчок.
// Если hoverClip/clickClip не заданы, звуки генерируются в коде (тихо, без резких верхов).
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0, 1)] private float hoverVolume = 0.22f;
    [SerializeField, Range(0, 1)] private float clickVolume = 0.35f;
    [Tooltip("Использовать встроенные мягкие звуки вместо hoverClip/clickClip")]
    [SerializeField] private bool useSoftSounds = true;

    private AudioSource audioSource;
    private static AudioClip softHover, softClick;
    private static float lastHoverTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true; // звучит и в меню паузы

        if (softHover == null) softHover = MakeTick("UI Hover", 1450f, 0.06f, 70f, 0.5f);
        if (softClick == null) softClick = MakeTick("UI Click", 720f, 0.12f, 38f, 0.8f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var b = GetComponent<Button>();
        if (b != null && !b.interactable) return;
        // Защита от "пулемёта", когда мышь быстро проходит по нескольким кнопкам
        if (Time.unscaledTime - lastHoverTime < 0.05f) return;
        lastHoverTime = Time.unscaledTime;

        var clip = useSoftSounds || hoverClip == null ? softHover : hoverClip;
        audioSource.pitch = Random.Range(0.97f, 1.03f);
        audioSource.PlayOneShot(clip, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var clip = useSoftSounds || clickClip == null ? softClick : clickClip;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, clickVolume);
    }

    // Короткий тон с мягкой атакой и быстрым затуханием + чуть-чуть шума "касания"
    static AudioClip MakeTick(string name, float freq, float length, float decay, float noise)
    {
        const int rate = 44100;
        int n = Mathf.RoundToInt(rate * length);
        var d = new float[n];
        var rnd = new System.Random(name.Length * 31);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float attack = Mathf.Clamp01(t / 0.003f);
            float env = attack * Mathf.Exp(-t * decay);
            float f = freq * (1f - 0.08f * t / length); // лёгкое падение высоты
            float tone = Mathf.Sin(t * 2f * Mathf.PI * f) + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * f * 2f);
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.15f;
            d[i] = (tone * 0.7f + lp * noise * Mathf.Exp(-t * 400f)) * env * 0.5f;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
