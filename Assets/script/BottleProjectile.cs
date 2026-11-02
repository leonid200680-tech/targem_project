using UnityEngine;

// Брошенная бутылка. При ударе обо что угодно разбивается: звон стекла, разлетаются осколки.
// Если попала в бабку или разбилась рядом с ней - бабка на время сильно замедляется (GrannySlow).
[RequireComponent(typeof(Rigidbody))]
public class BottleProjectile : MonoBehaviour
{
    public Material shardMaterial;
    public int shards = 26;
    public float slowRadius = 2.5f;
    public float slowTime = 5f;
    [Range(0, 1)] public float slowFactor = 0.3f;

    private bool broken;
    private static AudioClip breakClip;

    void Start()
    {
        Destroy(gameObject, 8f);   // на всякий случай, если улетела в пустоту
    }

    void OnCollisionEnter(Collision c)
    {
        if (broken) return;
        broken = true;
        Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        Vector3 normal = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
        Shatter(point, normal, GetComponent<Rigidbody>().linearVelocity);

        // Попадание в бабку или рядом с ней
        var slow = c.collider.GetComponentInParent<GrannySlow>();
        if (slow == null)
            foreach (var s in FindObjectsByType<GrannySlow>(FindObjectsSortMode.None))
                if (Vector3.Distance(s.transform.position, point) < slowRadius) { slow = s; break; }
        if (slow != null) slow.Slow(slowTime, slowFactor);

        Destroy(gameObject);
    }

    void Shatter(Vector3 point, Vector3 normal, Vector3 velocity)
    {
        if (breakClip == null) breakClip = MakeBreak();
        var src = new GameObject("Glass Break").AddComponent<AudioSource>();
        src.transform.position = point;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 30f;
        src.pitch = Random.Range(0.9f, 1.1f);
        src.PlayOneShot(breakClip, 1f);
        Destroy(src.gameObject, 2f);

        var mat = shardMaterial != null ? shardMaterial : GetComponentInChildren<Renderer>()?.sharedMaterial;
        Vector3 bounce = Vector3.Reflect(velocity, normal) * 0.25f;
        for (int i = 0; i < shards; i++)
        {
            var s = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Quad : PrimitiveType.Cube);
            s.name = "Glass Shard";
            Destroy(s.GetComponent<Collider>());
            // Коллайдер толще самого осколка, иначе тонкое стекло проваливается сквозь пол
            var box = s.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 4f);
            float size = Random.Range(0.012f, 0.045f);
            s.transform.localScale = new Vector3(size, size * Random.Range(0.5f, 1.6f), size * 0.15f);
            s.transform.SetPositionAndRotation(point + normal * 0.03f + Random.insideUnitSphere * 0.05f, Random.rotation);
            var r = s.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = s.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.4f;
            rb.linearVelocity = bounce + (normal * Random.Range(0.8f, 2.5f) + Random.insideUnitSphere * 2.2f);
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            s.AddComponent<GlassShard>();
        }
    }

    // Звон разбитого стекла: удар + много коротких высоких звяканий осколков
    static AudioClip MakeBreak()
    {
        const int rate = 44100;
        int n = (int)(rate * 1.3f);
        var d = new float[n];
        var rnd = new System.Random(77);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rnd.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.7f;
            d[i] = (w - lp) * Mathf.Exp(-t * 18f) * 0.9f;   // хруст удара
        }
        for (int k = 0; k < 60; k++)   // звяканья осколков
        {
            float start = (float)rnd.NextDouble() * (k < 20 ? 0.15f : 0.9f);
            float f = 2500f + (float)rnd.NextDouble() * 5500f;
            float amp = (0.15f + (float)rnd.NextDouble() * 0.35f) * (k < 20 ? 1f : 0.5f);
            int s0 = (int)(start * rate);
            for (int i = 0; i < rate / 8 && s0 + i < n; i++)
            {
                float t = i / (float)rate;
                d[s0 + i] += Mathf.Sin(t * f * 2f * Mathf.PI) * Mathf.Exp(-t * 45f) * amp;
            }
        }
        float peak = 0f;
        foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < n; i++) d[i] *= 0.9f / peak;
        var c = AudioClip.Create("Glass Break", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}

// Осколок: полежит и исчезнет, уменьшаясь
public class GlassShard : MonoBehaviour
{
    float born;
    Vector3 scale;

    void Start() { born = Time.time; scale = transform.localScale; }

    void Update()
    {
        float age = Time.time - born;
        if (age > 6f) transform.localScale = scale * Mathf.Max(0f, 1f - (age - 6f));
        if (age > 7f) Destroy(gameObject);
    }
}
