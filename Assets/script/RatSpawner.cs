using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Крысы в здании (для фона): время от времени неподалёку от игрока 1-3 крысы выскакивают
// из-под стены, перебегают вдоль стен по навмешу и прячутся. Чем выше страх - тем чаще.
// Писк и шуршание лапок синтезируются в коде. Модель крысы - Rat by Quaternius (CC0).
public class RatSpawner : MonoBehaviour
{
    public Rat ratPrefab;
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));

    [Header("Как часто")]
    [Tooltip("Пауза между появлениями крыс, сек (при нулевом страхе)")]
    public Vector2 interval = new Vector2(35f, 80f);
    [Tooltip("Во сколько раз чаще на максимальном страхе")]
    public float fearSpeedup = 2f;
    public int maxGroup = 3;

    [Header("Где")]
    public Vector2 spawnDistance = new Vector2(4f, 11f);
    public Vector2 runLength = new Vector2(4f, 10f);
    public LayerMask groundMask = ~0;

    private float nextSpawn;
    private PlayerController player;
    private AudioClip[] squeaks;
    private AudioClip patter;
    private readonly List<Vector3> corners = new List<Vector3>();

    void Start()
    {
        squeaks = new AudioClip[4];
        for (int i = 0; i < squeaks.Length; i++) squeaks[i] = MakeSqueak(i);
        patter = MakePatter();
        nextSpawn = Time.time + Random.Range(15f, 30f);
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy) player = FindAnyObjectByType<PlayerController>();
        if (player == null || ratPrefab == null || Time.time < nextSpawn) return;

        var qm = QuestManager.Instance;
        bool ok = player.enabled && !DialogueManager.IsDialogueActive && buildingZone.Contains(player.transform.position)
            && (qm == null || qm.CurrentStep < QuestManager.Step.Done);
        if (!ok) { nextSpawn = Time.time + 5f; return; }

        nextSpawn = Time.time + Random.Range(interval.x, interval.y) / Mathf.Lerp(1f, fearSpeedup, FearMeter.Level);
        if (!TrySpawn()) nextSpawn = Time.time + 4f;   // не нашлось места - попробуем скоро снова
    }

    // Для проверки из редактора
    public bool SpawnNow() => TrySpawn();

    bool TrySpawn()
    {
        Vector3 p = player.transform.position;
        var cam = Camera.main != null ? Camera.main.transform : player.transform;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            // Старт: у стены, не прямо перед глазами - чтобы крыса не появлялась из воздуха
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(spawnDistance.x, spawnDistance.y);
            if (!EdgePoint(p + new Vector3(r.x, 0f, r.y), p.y, out Vector3 start)) continue;
            Vector3 toStart = (start - cam.position).normalized;
            if (Vector3.Dot(toStart, cam.forward) > 0.6f && !Physics.Linecast(cam.position, start + Vector3.up * 0.1f, groundMask, QueryTriggerInteraction.Ignore))
                continue;

            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(runLength.x, runLength.y);
            if (!EdgePoint(start + new Vector3(d.x, 0f, d.y), p.y, out Vector3 end)) continue;

            var navPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, navPath) || navPath.status != NavMeshPathStatus.PathComplete) continue;
            if (!BuildCorners(navPath.corners, p.y)) continue;

            int count = Random.Range(1, maxGroup + 1);
            for (int i = 0; i < count; i++) SpawnRat(i * Random.Range(0.15f, 0.45f));
            return true;
        }
        return false;
    }

    bool EdgePoint(Vector3 around, float floorY, out Vector3 point)
    {
        point = default;
        if (!NavMesh.SamplePosition(around, out var hit, 2f, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(hit.position.y - floorY) > 1.3f || !buildingZone.Contains(hit.position)) return false;
        if (!NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(edge.position.y - floorY) > 1.3f) return false;
        point = edge.position;
        return true;
    }

    // Путь ровно по полу: каждую точку опускаем лучом на пол. Лестницы пропускаем.
    bool BuildCorners(Vector3[] navCorners, float floorY)
    {
        corners.Clear();
        float length = 0f;
        foreach (var c in navCorners)
        {
            if (Mathf.Abs(c.y - floorY) > 1.3f) return false;
            Vector3 q = c;
            if (Physics.Raycast(c + Vector3.up * 0.5f, Vector3.down, out var hit, 1.2f, groundMask, QueryTriggerInteraction.Ignore)) q = hit.point;
            if (corners.Count > 0) length += Vector3.Distance(corners[corners.Count - 1], q);
            corners.Add(q);
        }
        return corners.Count >= 2 && length < 20f;
    }

    void SpawnRat(float delay)
    {
        var points = new List<Vector3>(corners);
        if (delay <= 0f) Launch(points);
        else StartCoroutine(LaunchLater(points, delay));
    }

    System.Collections.IEnumerator LaunchLater(List<Vector3> points, float delay)
    {
        yield return new WaitForSeconds(delay);
        Launch(points);
    }

    void Launch(List<Vector3> points)
    {
        var rat = Instantiate(ratPrefab, points[0], Quaternion.identity, transform);
        float size = Random.Range(0.85f, 1.15f);
        rat.transform.localScale = Vector3.one * size;
        rat.speed *= Random.Range(0.85f, 1.15f);
        rat.squeaks = squeaks;
        rat.patter = patter;
        rat.gameObject.SetActive(true);
        rat.Run(points, player.transform);
    }

    // ---------- Синтез звуков ----------

    // Писк: 2-4 коротких высоких "чирика" с плавающей высотой
    static AudioClip MakeSqueak(int seed)
    {
        const int rate = 44100;
        var rnd = new System.Random(seed * 31 + 7);
        float Rand() => (float)rnd.NextDouble();
        int chirps = 2 + rnd.Next(3);
        var data = new List<float>();
        float phase = 0f;
        for (int c = 0; c < chirps; c++)
        {
            float dur = 0.05f + Rand() * 0.07f;
            float f0 = 3200f + Rand() * 1500f, f1 = f0 * (1.15f + Rand() * 0.35f);
            int n = (int)(dur * rate);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, Mathf.Sin(u * Mathf.PI)) * (1f + 0.02f * Mathf.Sin(u * 90f));
                phase += f / rate;
                float env = Mathf.Sin(u * Mathf.PI);
                env *= env;
                float s = Mathf.Sin(phase * 2f * Mathf.PI) * 0.8f + Mathf.Sin(phase * 4f * Mathf.PI) * 0.15f;
                data.Add(s * env * 0.6f);
            }
            int gap = (int)((0.02f + Rand() * 0.05f) * rate);
            for (int i = 0; i < gap; i++) data.Add(0f);
        }
        var clip = AudioClip.Create("Rat Squeak", data.Count, 1, rate, false);
        clip.SetData(data.ToArray(), 0);
        return clip;
    }

    // Шуршание лапок: частые тихие царапающие щелчки (луп)
    static AudioClip MakePatter()
    {
        const int rate = 22050;
        int n = rate;
        var data = new float[n];
        var rnd = new System.Random(5);
        float Rand() => (float)rnd.NextDouble();
        int at = 0;
        while (at < n)
        {
            int len = (int)(rate * (0.006f + Rand() * 0.01f));
            float amp = 0.25f + Rand() * 0.35f;
            float lp = 0f;
            for (int i = 0; i < len && at + i < n; i++)
            {
                float white = Rand() * 2f - 1f;
                lp += (white - lp) * 0.6f;
                data[at + i] += (white - lp) * amp * (1f - i / (float)len);
            }
            at += (int)(rate * (0.028f + Rand() * 0.03f));
        }
        var clip = AudioClip.Create("Rat Patter", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.4f, 0.3f, 0.4f);
        Gizmos.DrawWireCube(buildingZone.center, buildingZone.size);
    }
}
