using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Оформление кнопки меню: тёмный мазок краски под текстом, при наведении он наливается кровью,
// текст светлеет, вздрагивает и сдвигается. Работает и на паузе (Time.timeScale = 0).
[RequireComponent(typeof(Button))]
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public Image plate;
    public TMP_Text label;

    [Header("Цвета")]
    public Color plateNormal = new Color(0.012f, 0.002f, 0.002f, 0.93f);
    public Color plateHover = new Color(0.32f, 0.008f, 0.008f, 0.97f);
    public Color textNormal = new Color(0.86f, 0.8f, 0.72f, 1f);
    public Color textHover = new Color(1f, 0.95f, 0.9f, 1f);

    [Header("Движение")]
    public float hoverShift = 14f;
    public float speed = 14f;

    private bool hovered;
    private bool pressed;
    private float hover;           // 0..1, плавно
    private float flickerUntil;
    private Vector2 labelBasePos;

    void Awake()
    {
        if (plate == null) plate = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null) labelBasePos = label.rectTransform.anchoredPosition;
        Apply();
    }

    void OnDisable()
    {
        hovered = pressed = false;
        hover = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) => SetHover(true);
    public void OnPointerExit(PointerEventData e) { SetHover(false); pressed = false; }
    public void OnSelect(BaseEventData e) => SetHover(true);
    public void OnDeselect(BaseEventData e) => SetHover(false);
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    void SetHover(bool on)
    {
        if (on && !hovered) flickerUntil = Time.unscaledTime + 0.18f; // вздрагивает при наведении
        hovered = on;
    }

    void Update()
    {
        hover = Mathf.MoveTowards(hover, hovered ? 1f : 0f, Time.unscaledDeltaTime * speed * 0.6f);
        Apply();
    }

    void Apply()
    {
        float h = hover * hover * (3f - 2f * hover);
        bool flicker = Time.unscaledTime < flickerUntil && Random.value < 0.5f;

        if (plate != null)
        {
            plate.color = Color.Lerp(plateNormal, plateHover, h);
            float sx = Mathf.Lerp(1f, 1.06f, h) * (pressed ? 0.97f : 1f);
            plate.rectTransform.localScale = new Vector3(sx, pressed ? 0.95f : 1f, 1f);
        }

        if (label != null)
        {
            Color c = Color.Lerp(textNormal, textHover, h);
            if (flicker) c.a *= 0.35f;
            label.color = c;
            float jitter = flicker ? Random.Range(-3f, 3f) : 0f;
            label.rectTransform.anchoredPosition = labelBasePos + new Vector2(hoverShift * h + jitter, 0f);
        }
    }
}
