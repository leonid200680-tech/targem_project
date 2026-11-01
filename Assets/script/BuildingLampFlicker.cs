using System.Collections;
using UnityEngine;

// Тусклая умирающая лампа: почти всё время горит с лёгким "дыханием" яркости,
// иногда срывается в серию морганий, иногда гаснет на пару секунд.
// Гудение (3D, тихое) идёт вместе со светом и пропадает, когда лампа гаснет.
public class BuildingLampFlicker : MonoBehaviour
{
    public Light lamp;
    public float intensity = 60f;                 // яркость "горит" (в единицах света)
    [Range(0, 1)] public float hum = 0.08f;       // мелкое дрожание яркости
    [Range(0, 1)] public float offLevel = 0.04f;  // остаток свечения, когда моргнула

    [Header("Мерцание")]
    public Vector2 burstInterval = new Vector2(4f, 14f);
    public Vector2Int burstFlashes = new Vector2Int(2, 6);
    [Range(0, 1)] public float dropoutChance = 0.25f;
    public Vector2 dropoutLength = new Vector2(0.8f, 2.5f);
    [Tooltip("Сломанная лампа: моргает часто и нервно")]
    public bool broken;

    [Header("Гудение")]
    public AudioSource buzz;
    [Range(0, 1)] public float buzzVolume = 0.15f;

    private float state = 1f;   // 1 - горит, offLevel - моргнула
    private float seed;

    void Start()
    {
        if (lamp == null) lamp = GetComponentInChildren<Light>();
        seed = Random.value * 100f;
        if (buzz != null)
        {
            buzz.loop = true;
            buzz.time = Random.Range(0f, buzz.clip != null ? buzz.clip.length : 0f);
            buzz.pitch = Random.Range(0.92f, 1.06f);
            buzz.Play();
        }
        StartCoroutine(FlickerRoutine());
    }

    void Update()
    {
        float t = Time.time + seed;
        float noise = Mathf.PerlinNoise(t * 7f, seed) * 0.7f + Mathf.PerlinNoise(t * 31f, seed + 3f) * 0.3f;
        float k = state * (1f - hum + hum * noise);
        if (lamp != null) lamp.intensity = intensity * k;
        if (buzz != null) buzz.volume = buzzVolume * (state > 0.5f ? 1f : 0f);
    }

    IEnumerator FlickerRoutine()
    {
        // Разные лампы начинают в разное время, чтобы не моргали хором
        yield return new WaitForSeconds(Random.Range(0f, burstInterval.y));

        while (true)
        {
            float wait = broken ? Random.Range(0.6f, 3f) : Random.Range(burstInterval.x, burstInterval.y);
            yield return new WaitForSeconds(wait);

            // Серия морганий
            int flashes = Random.Range(burstFlashes.x, burstFlashes.y + 1);
            for (int i = 0; i < flashes; i++)
            {
                state = offLevel;
                yield return new WaitForSeconds(Random.Range(0.03f, broken ? 0.25f : 0.12f));
                state = Random.Range(0.6f, 1f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.18f));
            }
            state = 1f;

            // Иногда лампа гаснет надолго - и в темноте остаётся только тишина
            if (Random.value < dropoutChance)
            {
                state = 0f;
                yield return new WaitForSeconds(Random.Range(dropoutLength.x, dropoutLength.y));
                // Загорается неуверенно
                for (int i = 0; i < 3; i++)
                {
                    state = Random.Range(0.3f, 0.8f);
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
                    state = offLevel;
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.15f));
                }
                state = 1f;
            }
        }
    }
}
