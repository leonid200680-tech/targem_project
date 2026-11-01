using TMPro;
using UnityEngine;

// Название игры в меню: медленно "дышит" яркостью и изредка даёт сбой -
// вздрагивает, гаснет на мгновение и расслаивается, как испорченная плёнка.
[RequireComponent(typeof(TMP_Text))]
public class MenuTitleFx : MonoBehaviour
{
    [Range(0, 1)] public float breathe = 0.12f;
    public Vector2 glitchInterval = new Vector2(3.5f, 8f);
    public float glitchStrength = 10f;

    private TMP_Text text;
    private Color baseColor;
    private Vector2 basePos;
    private float nextGlitch;
    private float glitchUntil;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseColor = text.color;
        basePos = text.rectTransform.anchoredPosition;
        nextGlitch = Time.unscaledTime + Random.Range(glitchInterval.x, glitchInterval.y);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float a = 1f - breathe * (0.5f + 0.5f * Mathf.Sin(t * 1.3f)) - breathe * 0.5f * Mathf.PerlinNoise(t * 4f, 0.3f);

        if (t >= nextGlitch)
        {
            glitchUntil = t + Random.Range(0.08f, 0.22f);
            nextGlitch = t + Random.Range(glitchInterval.x, glitchInterval.y);
        }

        Vector2 offset = Vector2.zero;
        if (t < glitchUntil)
        {
            offset = new Vector2(Random.Range(-glitchStrength, glitchStrength), Random.Range(-2f, 2f));
            if (Random.value < 0.4f) a *= 0.2f;
            text.characterSpacing = Random.Range(8f, 20f);
        }
        else text.characterSpacing = 12f;

        text.rectTransform.anchoredPosition = basePos + offset;
        text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * a);
    }
}
