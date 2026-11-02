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

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float baseVolume;
    private float inside;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
    }

    void Update()
    {
        var cam = Camera.main;
        bool listenerInside = cam != null && buildingZone.Contains(cam.transform.position);
        inside = Mathf.MoveTowards(inside, listenerInside ? 1f : 0f, Time.deltaTime * fadeSpeed);

        source.volume = baseVolume * Mathf.Lerp(1f, insideVolume, inside);
        // Частота среза меняется по экспоненте, чтобы переход на слух был ровным
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(insideCutoff), inside));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireCube(buildingZone.center, buildingZone.size);
    }
}
