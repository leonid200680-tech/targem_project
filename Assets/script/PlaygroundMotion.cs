using UnityEngine;

// Качели и карусель у конторы, движение подогнано под их звук (AudioSource со скрипом).
// В записи скрипы идут сериями; моменты скрипов (creakTimes, найдены по громкости записи) -
// это крайние точки качания: между соседними скрипами сиденье проходит из одного края в другой.
// В тишине между сериями качели затухают и почти останавливаются, перед новой серией снова раскачиваются.
// Карусель на сериях скрипов разгоняется, в тишине катится по инерции и замедляется.
public class PlaygroundMotion : MonoBehaviour
{
    public enum Kind { Swing, Carousel }

    public Kind kind = Kind.Swing;
    public AudioSource source;
    [Tooltip("Моменты скрипов в записи, сек")]
    public float[] creakTimes;
    [Tooltip("Обычный промежуток между скрипами в серии (полпериода качания), сек")]
    public float halfPeriod = 0.74f;

    [Header("Качели")]
    public Transform[] seats;           // сиденья с цепями, качаются вокруг верхней перекладины
    public Vector3 barAxis = Vector3.right;
    public float maxAngle = 30f;
    public float restAngle = 3f;        // лёгкое покачивание в тишине (ветер)

    [Header("Карусель")]
    public Transform[] spinning;
    public Vector3 spinCenter;
    public float maxSpeed = 55f;        // град/с во время серии скрипов
    public float minSpeed = 4f;

    private Transform[] pivots;
    private int[] extremeIndex;         // номер крайней точки для каждого скрипа
    private float clipLength;
    private float spinSpeed;

    void Start()
    {
        if (source == null) source = GetComponent<AudioSource>();
        clipLength = source != null && source.clip != null ? source.clip.length : 0f;
        if (creakTimes == null || creakTimes.Length == 0 || clipLength <= 0f) { enabled = false; return; }

        // Между скрипами - целое число полупериодов (обычно один: из края в край)
        extremeIndex = new int[creakTimes.Length];
        for (int i = 1; i < creakTimes.Length; i++)
            extremeIndex[i] = extremeIndex[i - 1] + Mathf.Max(1, Mathf.RoundToInt((creakTimes[i] - creakTimes[i - 1]) / halfPeriod));

        if (kind == Kind.Swing && seats != null)
        {
            // Каждое сиденье - под шарнир на верхней перекладине
            pivots = new Transform[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                var r = seats[i].GetComponent<Renderer>();
                Vector3 top = r != null ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z) : seats[i].position;
                pivots[i] = new GameObject(seats[i].name + " Pivot").transform;
                pivots[i].SetParent(seats[i].parent, false);
                pivots[i].position = top;
                pivots[i].rotation = Quaternion.identity;
                seats[i].SetParent(pivots[i], true);
            }
        }
    }

    // Время по записи (с учётом зацикливания)
    float AudioTime => source.isPlaying ? source.time : Time.time % clipLength;

    // Ближайшие скрипы до и после момента t (через границу зацикливания)
    void Neighbours(float t, out float t0, out float t1, out int m0, out int m1)
    {
        int n = creakTimes.Length;
        int lastExtreme = extremeIndex[n - 1];
        int i = 0;
        while (i < n && creakTimes[i] <= t) i++;
        if (i == 0)
        {
            t0 = creakTimes[n - 1] - clipLength; m0 = lastExtreme - Wrap();
            t1 = creakTimes[0]; m1 = 0;
        }
        else if (i == n)
        {
            t0 = creakTimes[n - 1]; m0 = lastExtreme;
            t1 = creakTimes[0] + clipLength; m1 = lastExtreme + Wrap();
        }
        else
        {
            t0 = creakTimes[i - 1]; m0 = extremeIndex[i - 1];
            t1 = creakTimes[i]; m1 = extremeIndex[i];
        }
    }

    int Wrap()
    {
        float gap = creakTimes[0] + clipLength - creakTimes[creakTimes.Length - 1];
        return Mathf.Max(1, Mathf.RoundToInt(gap / halfPeriod));
    }

    // Насколько сильно сейчас раскачано: 1 рядом со скрипами, затухает в тишине
    static float Energy(float t, float t0, float t1)
    {
        float d = Mathf.Min(t - t0, t1 - t);
        return Mathf.Exp(-Mathf.Max(0f, d - 0.4f) / 1.6f);
    }

    void Update()
    {
        float t = AudioTime;
        Neighbours(t, out float t0, out float t1, out int m0, out int m1);
        float k = Mathf.Clamp01((t - t0) / Mathf.Max(0.01f, t1 - t0));
        float energy = Energy(t, t0, t1);

        if (kind == Kind.Swing && pivots != null)
        {
            // Фаза: в каждом скрипе - крайняя точка (cos = ±1)
            float phase = Mathf.PI * Mathf.Lerp(m0, m1, k);
            float amp = Mathf.Lerp(restAngle, maxAngle, energy);
            float angle = amp * Mathf.Cos(phase);
            Quaternion q = Quaternion.AngleAxis(angle, barAxis.normalized);
            // Шарниры созданы с нулевым поворотом в мире, ось перекладины - тоже в мире
            for (int i = 0; i < pivots.Length; i++)
                pivots[i].rotation = q;
        }
        else if (kind == Kind.Carousel && spinning != null)
        {
            float target = Mathf.Lerp(minSpeed, maxSpeed, energy);
            // разгон быстрый (толкнули), торможение медленное (инерция)
            spinSpeed = Mathf.MoveTowards(spinSpeed, target, Time.deltaTime * (target > spinSpeed ? 40f : 8f));
            foreach (var s in spinning)
                s.RotateAround(spinCenter, Vector3.up, spinSpeed * Time.deltaTime);
        }
    }
}
