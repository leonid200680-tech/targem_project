using UnityEngine;

// Видимый огонёк зажигалки: маленький язычок пламени (синий у основания, жёлто-оранжевый сверху),
// всегда повёрнут к камере и мерцает вместе со светом. Вешается на объект со светом огня
// и сам включается/выключается вместе с этим светом.
[RequireComponent(typeof(Light))]
public class LighterFlameFx : MonoBehaviour
{
    public float height = 0.03f;          // высота язычка, м
    public float flicker = 0.18f;
    [Tooltip("Материал-образец (HDRP/Unlit, аддитивный) - чтобы шейдер точно попал в билд")]
    public Material flameMaterial;

    private Light flameLight;
    private float baseIntensity;
    private Transform flame;
    private Material mat;
    private float seed;

    void Awake()
    {
        flameLight = GetComponent<Light>();
        baseIntensity = flameLight.intensity;
        seed = Random.value * 10f;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Flame";
        flame = quad.transform;
        flame.SetParent(transform, false);
        var r = quad.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        mat = flameMaterial != null ? new Material(flameMaterial) : new Material(Shader.Find("HDRP/Unlit"));
        mat.SetTexture("_UnlitColorMap", MakeFlameTexture());
        r.sharedMaterial = mat;
    }

    void LateUpdate()
    {
        bool on = flameLight.enabled;
        if (flame.gameObject.activeSelf != on) flame.gameObject.SetActive(on);
        if (!on) return;

        float t = Time.time * 9f + seed;
        float k = 1f + (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * flicker;
        flameLight.intensity = baseIntensity * k;

        // Масштаб в мире не зависит от масштаба зажигалки
        Vector3 parentScale = transform.lossyScale;
        float h = height * (0.9f + 0.2f * Mathf.PerlinNoise(t * 1.3f, 2f));
        float w = height * 0.42f;
        flame.localScale = new Vector3(w / parentScale.x, h / parentScale.y, 1f / parentScale.z);

        // Стоит вертикально (по миру), основание - у фитиля, лицом к камере
        var cam = Camera.main;
        flame.position = transform.position + Vector3.up * h * 0.45f;
        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - flame.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f) flame.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }
        // лёгкое покачивание
        flame.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 0.7f, 5f) - 0.5f) * 12f);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    // Капля пламени: синее ядро внизу, выше жёлтое и оранжевое, мягкие края
    static Texture2D MakeFlameTexture()
    {
        const int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;   // -1..1
                float v = (y + 0.5f) / h;              // 0 низ .. 1 верх
                // ширина капли: шире внизу, к верху сходится в острие
                float width = Mathf.Sin(Mathf.Clamp01(v * 1.15f) * Mathf.PI) * (1f - v * 0.55f);
                float d = Mathf.Abs(u) / Mathf.Max(0.001f, width);
                float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(v * 6f) * Mathf.Clamp01((1f - v) * 3f);
                a = a * a;
                Color c = Color.Lerp(new Color(0.25f, 0.45f, 1f), new Color(1f, 0.85f, 0.4f), Mathf.Clamp01((v - 0.15f) * 3f));
                c = Color.Lerp(c, new Color(1f, 0.45f, 0.1f), Mathf.Clamp01((v - 0.55f) * 2.2f) * (0.5f + 0.5f * d));
                px[y * w + x] = c * a;   // для наложения: цвет уже умножен на прозрачность
                px[y * w + x].a = a;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
