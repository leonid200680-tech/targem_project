using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Кровавые подтёки под буквами заголовка: стекают с нижних штрихов букв, медленно
// удлиняются, на конце набухает капля и иногда срывается вниз. Позиции берутся из
// реальной геометрии текста TMP, поэтому совпадают с любым шрифтом и размером.
[RequireComponent(typeof(TMP_Text))]
public class MenuTitleDrips : MonoBehaviour
{
    [Tooltip("Темнее надписи, иначе на красном фоне подтёки сливаются")]
    public Color bloodColor = new Color(0.13f, 0.0f, 0.0f, 1f);
    [Tooltip("Сколько подтёков на букву в среднем")]
    [Range(0, 2)] public float dripsPerLetter = 0.9f;
    public Vector2 lengthRange = new Vector2(40f, 170f);
    public Vector2 widthRange = new Vector2(10f, 18f);
    public float growSpeed = 9f;          // пикселей в секунду, кровь тягучая
    public Vector2 fallInterval = new Vector2(4f, 12f);
    public int seed = 13;

    // Где у буквы нижние штрихи (доля ширины глифа); по умолчанию - центр
    static readonly Dictionary<char, float[]> Stems = new Dictionary<char, float[]>
    {
        { 'К', new[] { 0.16f, 0.86f } }, { 'Н', new[] { 0.16f, 0.84f } }, { 'Т', new[] { 0.5f } },
        { 'О', new[] { 0.42f, 0.58f } }, { 'Р', new[] { 0.2f } },          { 'А', new[] { 0.1f, 0.9f } },
        { 'П', new[] { 0.16f, 0.84f } }, { 'У', new[] { 0.45f } },          { 'З', new[] { 0.45f } },
    };

    class Drip
    {
        public RectTransform stream;
        public Image streamImg;
        public RectTransform drop;
        public Image dropImg;
        public float length, target, width, nextFall;
        public RectTransform falling;
        public float fallY, fallSpeed;
        public Vector2 origin;
    }

    private TMP_Text text;
    private readonly List<Drip> drips = new List<Drip>();
    private static Sprite streamSprite, dropSprite;
    private System.Random rnd;
    private bool built;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        rnd = new System.Random(seed);
    }

    void Start() => Build();

    void Build()
    {
        if (built) return;
        built = true;
        text.ForceMeshUpdate();
        var info = text.textInfo;

        var layer = new GameObject("Blood Drips", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(transform, false);
        layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.sizeDelta = Vector2.zero;
        layer.SetAsFirstSibling();

        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            char c = char.ToUpperInvariant(ch.character);
            float[] stems = Stems.TryGetValue(c, out var s) ? s : new[] { 0.5f };

            foreach (float stem in stems)
            {
                if (rnd.NextDouble() > dripsPerLetter / stems.Length * 1.1f) continue;
                float x = Mathf.Lerp(ch.bottomLeft.x, ch.bottomRight.x, stem) + R(-3f, 3f);
                float y = ch.baseLine + ch.pointSize * 0.06f;   // чуть выше линии, чтобы подтёк выходил из буквы
                drips.Add(MakeDrip(layer, new Vector2(x, y)));
            }
        }
    }

    Drip MakeDrip(RectTransform parent, Vector2 origin)
    {
        var d = new Drip { origin = origin };
        d.width = R(widthRange.x, widthRange.y);
        d.target = R(lengthRange.x, lengthRange.y);
        d.length = d.target * R(0.15f, 0.6f);          // часть уже натекла к моменту открытия меню
        d.nextFall = Time.unscaledTime + R(fallInterval.x, fallInterval.y);

        d.streamImg = NewImage("Drip", parent, GetStreamSprite());
        d.stream = d.streamImg.rectTransform;
        d.stream.pivot = new Vector2(0.5f, 1f);
        d.stream.anchoredPosition = origin;

        d.dropImg = NewImage("Drop", parent, GetDropSprite());
        d.drop = d.dropImg.rectTransform;
        d.drop.pivot = new Vector2(0.5f, 0.75f);
        return d;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float alpha = text.color.a;   // подтёки гаснут вместе с надписью при сбоях
        Color c = new Color(bloodColor.r, bloodColor.g, bloodColor.b, bloodColor.a * Mathf.Clamp01(alpha * 1.15f));

        foreach (var d in drips)
        {
            // Медленно натекает, у конца замедляется
            float k = 1f - d.length / d.target;
            d.length = Mathf.Min(d.target, d.length + growSpeed * Mathf.Max(k, 0.08f) * Time.unscaledDeltaTime);

            float w = d.width;
            d.stream.sizeDelta = new Vector2(w, d.length);
            d.streamImg.color = c;

            // Капля на конце набухает перед тем, как сорваться
            float swell = 1f + 0.35f * Mathf.Clamp01(1f - (d.nextFall - t) / 2f);
            d.drop.sizeDelta = new Vector2(w * 1.5f * swell, w * 1.7f * swell);
            d.drop.anchoredPosition = d.origin + new Vector2(0f, -d.length);
            d.dropImg.color = c;

            if (t >= d.nextFall && d.length > d.target * 0.5f)
            {
                d.nextFall = t + R(fallInterval.x, fallInterval.y);
                if (d.falling == null)
                {
                    var img = NewImage("Falling Drop", (RectTransform)d.stream.parent, GetDropSprite());
                    d.falling = img.rectTransform;
                    d.falling.pivot = new Vector2(0.5f, 0.5f);
                }
                d.falling.gameObject.SetActive(true);
                d.fallY = 0f;
                d.fallSpeed = 0f;
                d.length *= 0.85f; // подтёк чуть укорачивается
            }

            if (d.falling != null && d.falling.gameObject.activeSelf)
            {
                d.fallSpeed += 1400f * Time.unscaledDeltaTime;
                d.fallY += d.fallSpeed * Time.unscaledDeltaTime;
                d.falling.anchoredPosition = d.origin + new Vector2(0f, -d.length - 6f - d.fallY);
                d.falling.sizeDelta = new Vector2(w * 1.1f, w * 1.8f);
                var fc = c;
                fc.a *= Mathf.Clamp01(1f - d.fallY / 380f);
                d.falling.GetComponent<Image>().color = fc;
                if (d.fallY > 380f) d.falling.gameObject.SetActive(false);
            }
        }
    }

    float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    static Image NewImage(string name, RectTransform parent, Sprite sprite)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.sprite = sprite;
        img.raycastTarget = false;
        var shadow = img.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return img;
    }

    // Струйка: сверху шире (там, где кровь выходит из буквы), книзу сужается, мягкие края
    static Sprite GetStreamSprite()
    {
        if (streamSprite != null) return streamSprite;
        const int w = 32, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);                 // 0 - низ, 1 - верх
            float half = Mathf.Lerp(0.32f, 0.5f, Mathf.Pow(v, 3f));
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs(x / (w - 1f) - 0.5f);
                float a = Mathf.Clamp01((half - u) * 14f);
                // блик сбоку - кровь влажная
                float shine = Mathf.Exp(-Mathf.Pow((x / (w - 1f) - 0.36f) * 9f, 2f)) * 0.35f;
                float l = 0.7f + shine;
                px[y * w + x] = new Color(l, l, l, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        streamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
        return streamSprite;
    }

    // Капля: круглая снизу, вытянутая кверху
    static Sprite GetDropSprite()
    {
        if (dropSprite != null) return dropSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (s - 1f) - 0.5f, v = y / (s - 1f);
                // круг в нижней части + сужение кверху
                float r = v < 0.55f ? 0.3f : Mathf.Lerp(0.3f, 0.05f, (v - 0.55f) / 0.45f);
                float cy = v < 0.55f ? 0.3f : v;
                float dist = v < 0.55f ? Mathf.Sqrt(u * u + (v - cy) * (v - cy)) : Mathf.Abs(u);
                float a = Mathf.Clamp01((r - dist) * 30f);
                float shine = Mathf.Exp(-((u + 0.1f) * (u + 0.1f) + (v - 0.38f) * (v - 0.38f)) * 220f) * 0.6f;
                float l = 0.7f + shine * 0.5f;
                px[y * s + x] = new Color(l, l, l, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        dropSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return dropSprite;
    }
}
