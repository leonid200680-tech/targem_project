using UnityEngine;

// Щелчок кнопки фонарика: следит за светом и щёлкает, когда его включают или выключают.
// Звук генерируется в коде: короткий пластиковый щелчок, у выключения он чуть ниже.
public class FlashlightClick : MonoBehaviour
{
    public Light spotLight;
    [Range(0, 1)] public float volume = 0.55f;

    private AudioSource source;
    private AudioClip clickOn, clickOff;
    private bool wasOn;

    void Start()
    {
        if (spotLight == null)
        {
            var fl = GetComponent<Flashlight>();
            if (fl != null) spotLight = fl.spotLight;
        }
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        clickOn = MakeClick(1.0f, 11);
        clickOff = MakeClick(0.8f, 23);
        wasOn = spotLight != null && spotLight.enabled;
    }

    void Update()
    {
        if (spotLight == null) return;
        bool on = spotLight.enabled;
        if (on != wasOn)
        {
            source.pitch = Random.Range(0.96f, 1.04f);
            source.PlayOneShot(on ? clickOn : clickOff, volume);
            wasOn = on;
        }
    }

    // Два быстрых удара (нажатие и отскок кнопки) + короткий резонанс корпуса
    static AudioClip MakeClick(float pitch, int seed)
    {
        const int rate = 44100;
        int n = rate / 10;
        var d = new float[n];
        var rnd = new System.Random(seed);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.6f;
            float hit1 = (white - lp) * Mathf.Exp(-t * 900f);
            float t2 = t - 0.018f;
            float hit2 = t2 > 0 ? (white - lp) * Mathf.Exp(-t2 * 1100f) * 0.6f : 0f;
            float body = Mathf.Sin(t * 2f * Mathf.PI * 1800f * pitch) * Mathf.Exp(-t * 260f) * 0.35f
                       + (t2 > 0 ? Mathf.Sin(t2 * 2f * Mathf.PI * 2300f * pitch) * Mathf.Exp(-t2 * 300f) * 0.25f : 0f);
            d[i] = (hit1 + hit2 + body) * 0.7f;
        }
        var clip = AudioClip.Create(pitch >= 1f ? "Flashlight On" : "Flashlight Off", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
