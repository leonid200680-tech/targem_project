using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// Вместо строки "Нажмите E, чтобы ..." показывает клавишу E в хоррор-стиле
// и короткую подпись действия под ней. Вешается на тот же объект, что и InteractionText:
// PlayerInteraction продолжает писать текст и включать/выключать объект как раньше.
[RequireComponent(typeof(Text))]
public class InteractionPrompt : MonoBehaviour
{
    [Header("Шрифты (если пусто - шрифт исходного текста)")]
    public Font keyFont;
    public Font labelFont;

    [Header("Вид")]
    public float keySize = 54f;
    public float tilt = -4f; // клавиша слегка "криво прибита"
    public Color keyColor = new Color(0.78f, 0.06f, 0.04f);
    public Color labelColor = new Color(0.74f, 0.68f, 0.58f, 0.9f);
    public bool showActionLabel = true;
    public Vector2 offset = new Vector2(0f, -70f); // от центра экрана вниз, чтобы не закрывать прицел

    [Header("Мерцание")]
    public float flickerSpeed = 9f;
    [Range(0, 1)] public float flickerAmount = 0.35f;

    private Text source;
    private RectTransform root;
    private Image badge;
    private Text letter;
    private Text label;
    private Outline glow;
    private string lastText;
    private float shownTime;
    private float dropoutUntil;

    private static Sprite badgeSprite;

    static readonly Regex KeyPhrase = new Regex(@"^\s*(нажмите|нажми|press)\s+e\s*,?\s*(чтобы|to)?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    void Awake()
    {
        source = GetComponent<Text>();
        Build();
    }

    void OnEnable()
    {
        shownTime = Time.unscaledTime;
        lastText = null;
    }

    void Build()
    {
        Font baseFont = source.font;

        root = new GameObject("HorrorPrompt", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(keySize, keySize);
        root.anchoredPosition = offset;

        badge = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        badge.rectTransform.SetParent(root, false);
        badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        badge.rectTransform.sizeDelta = new Vector2(keySize, keySize);
        badge.sprite = GetBadgeSprite();
        badge.raycastTarget = false;

        letter = new GameObject("KeyLetter", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        letter.rectTransform.SetParent(root, false);
        letter.rectTransform.sizeDelta = new Vector2(keySize, keySize);
        letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt * 0.5f);
        letter.font = keyFont != null ? keyFont : baseFont;
        letter.fontSize = Mathf.RoundToInt(keySize * 0.68f);
        letter.alignment = TextAnchor.MiddleCenter;
        letter.color = keyColor;
        letter.text = "E";
        letter.raycastTarget = false;
        glow = letter.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(0.35f, 0f, 0f, 0.8f);
        glow.effectDistance = new Vector2(1.5f, -1.5f);
        var shadow = letter.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -3f);

        label = new GameObject("ActionLabel", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.rectTransform.SetParent(root, false);
        label.rectTransform.sizeDelta = new Vector2(320f, 26f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -keySize * 0.5f - 16f);
        label.font = labelFont != null ? labelFont : baseFont;
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = labelColor;
        label.raycastTarget = false;
        var labelShadow = label.gameObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelShadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    void LateUpdate()
    {
        // Исходный текст не рисуем, но продолжаем его читать
        if (source.enabled) source.enabled = false;

        if (source.text != lastText)
        {
            lastText = source.text;
            string action = KeyPhrase.Replace(lastText ?? "", "");
            bool hasKey = action.Length != (lastText ?? "").Length;
            if (!hasKey) action = lastText; // строка без "E" - показываем как есть
            if (action.Length > 0) action = char.ToUpper(action[0]) + action.Substring(1);
            label.text = action;
            label.gameObject.SetActive(showActionLabel || !hasKey);
            badge.gameObject.SetActive(hasKey);
            letter.gameObject.SetActive(hasKey);
        }

        Animate();
    }

    void Animate()
    {
        float t = Time.unscaledTime;
        float age = t - shownTime;

        // Появление: клавиша "впечатывается" в экран
        float pop = Mathf.Clamp01(age / 0.18f);
        float scale = Mathf.Lerp(1.35f, 1f, 1f - (1f - pop) * (1f - pop));
        root.localScale = new Vector3(scale, scale, 1f);

        // Нервное мерцание + редкие провалы, как у умирающей лампы
        float n = Mathf.PerlinNoise(t * flickerSpeed, 0.37f);
        float alpha = 1f - flickerAmount * n;
        if (t > dropoutUntil && Random.value < 0.004f) dropoutUntil = t + Random.Range(0.04f, 0.12f);
        if (t < dropoutUntil) alpha *= 0.25f;
        alpha *= pop;

        SetAlpha(badge, alpha);
        SetAlpha(letter, alpha);
        SetAlpha(label, labelColor.a * Mathf.Lerp(0.85f, 1f, n) * pop);
        glow.effectColor = new Color(0.45f * (0.6f + 0.4f * n), 0f, 0f, 0.8f * alpha);

        // Лёгкая дрожь
        Vector2 jitter = new Vector2(Mathf.PerlinNoise(t * 23f, 1.1f) - 0.5f, Mathf.PerlinNoise(1.9f, t * 23f) - 0.5f) * 1.6f;
        root.anchoredPosition = offset + jitter;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    // Процедурная "грязная" клавиша: тёмная плашка, неровная кровавая рамка, царапины и подтёки
    static Sprite GetBadgeSprite()
    {
        if (badgeSprite != null) return badgeSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        var rnd = new System.Random(1666);

        Color fill = new Color(0.05f, 0.035f, 0.03f, 0.88f);
        Color blood = new Color(0.5f, 0.03f, 0.02f, 1f);
        Color bloodDark = new Color(0.25f, 0.01f, 0.01f, 1f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                // расстояние до края скруглённого квадрата
                float dx = Mathf.Max(Mathf.Abs(u - 0.5f) - 0.36f, 0f);
                float dy = Mathf.Max(Mathf.Abs(v - 0.5f) - 0.36f, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - 0.08f; // <0 внутри
                float rough = (Mathf.PerlinNoise(u * 14f, v * 14f) - 0.5f) * 0.035f;
                d += rough;

                Color c = new Color(0, 0, 0, 0);
                if (d < 0f)
                {
                    float grime = Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 7f);
                    c = Color.Lerp(fill, fill * 1.8f, grime * 0.5f);
                    c.a = fill.a;
                }
                // рамка
                float border = Mathf.Abs(d + 0.035f);
                float borderWidth = 0.018f + Mathf.PerlinNoise(u * 20f + 11f, v * 20f) * 0.02f;
                if (border < borderWidth && Mathf.PerlinNoise(u * 30f, v * 30f + 5f) > 0.22f)
                {
                    float k = Mathf.PerlinNoise(u * 40f, v * 40f);
                    c = Color.Lerp(bloodDark, blood, k);
                }
                px[y * size + x] = c;
            }

        // подтёки вниз с нижнего края рамки
        for (int i = 0; i < 4; i++)
        {
            int x0 = 22 + rnd.Next(84);
            int len = 8 + rnd.Next(22);
            int w = 2 + rnd.Next(3);
            for (int j = 0; j < len; j++)
            {
                int y = 14 - j;
                if (y < 1) break;
                int ww = j > len - 4 ? w + 1 : w; // капля на конце
                for (int k = -ww / 2; k <= ww / 2; k++)
                {
                    int x = Mathf.Clamp(x0 + k, 0, size - 1);
                    px[y * size + x] = Color.Lerp(blood, bloodDark, j / (float)len);
                }
            }
        }

        // царапины по плашке
        for (int i = 0; i < 7; i++)
        {
            float x = 24 + rnd.Next(80), y = 24 + rnd.Next(80);
            float ang = (float)rnd.NextDouble() * Mathf.PI;
            int len = 10 + rnd.Next(25);
            for (int j = 0; j < len; j++)
            {
                int xi = Mathf.RoundToInt(x + Mathf.Cos(ang) * j), yi = Mathf.RoundToInt(y + Mathf.Sin(ang) * j);
                if (xi < 0 || yi < 0 || xi >= size || yi >= size) break;
                Color c = px[yi * size + xi];
                if (c.a > 0.5f) px[yi * size + xi] = Color.Lerp(c, new Color(0.3f, 0.26f, 0.22f, c.a), 0.45f);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        badgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return badgeSprite;
    }
}
