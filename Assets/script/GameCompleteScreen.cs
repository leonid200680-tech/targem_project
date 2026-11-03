using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Экран "Игра пройдена": затемнение, кровавый заголовок, короткий эпилог, время прохождения,
// кнопки "В главное меню" и "Выход". Собирается из кода при первом показе.
public class GameCompleteScreen : MonoBehaviour
{
    [Header("Тексты")]
    public string title = "ИГРА ПРОЙДЕНА";
    public string subtitle = "Анджела спасена";
    [TextArea(2, 4)] public string epilogue = "Ритуал завершён, бабка изгнана. Анджела снова рядом с братом...\nпо крайней мере, так кажется.";
    public string footer = "Спасибо за игру  ·  On the Knee STUDIO";
    [Tooltip("Какая концовка (\"Концовка 1 из 3 · ...\"), задаётся системой рассудка")]
    public string endingLabel = "";

    [Header("Оформление")]
    public TMP_FontAsset titleFont;
    public Material titleMaterial;
    public TMP_FontAsset textFont;
    public Material textMaterial;
    public Sprite buttonSprite;
    public float fadeTime = 2.5f;

    [Header("Звук")]
    public AudioSource music;

    private Canvas canvas;
    private CanvasGroup group;
    private Image black;
    private bool shown;

    public void Show()
    {
        if (shown) return;
        shown = true;
        OutdoorSound.EndingSilence = true;
        gameObject.SetActive(true);
        StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        Build();

        // Сначала экран уходит в чёрный, потом проявляется текст
        black.color = new Color(0f, 0f, 0f, 0f);
        group.alpha = 0f;
        yield return Fade(fadeTime, k => black.color = new Color(0f, 0f, 0f, k));

        // Глушим игровые звуки и включаем музыку меню
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (src != music && src.isPlaying && src.loop) src.Stop();
        if (music != null) { music.volume = 0f; music.Play(); StartCoroutine(Fade(4f, k => music.volume = k)); }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return new WaitForSeconds(0.6f);
        yield return Fade(2f, k => group.alpha = k);
        group.interactable = group.blocksRaycasts = true;
    }

    IEnumerator Fade(float time, System.Action<float> apply)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        apply(1f);
    }

    void Build()
    {
        var cgo = new GameObject("Game Complete Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        black = NewImage("Black", cgo.transform);
        Stretch(black.rectTransform);
        black.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(cgo.transform, false);
        Stretch((RectTransform)content.transform);
        group = content.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        var root = content.transform;

        if (!string.IsNullOrEmpty(endingLabel))
        {
            var label = NewText("Ending", root, textFont, textMaterial, 30, endingLabel, new Color(0.62f, 0.56f, 0.5f, 0.85f), new Vector2(0, 390), new Vector2(1200, 50));
            label.characterSpacing = 8;
        }

        var t = NewText("Title", root, titleFont, titleMaterial, 120, title, new Color(0.48f, 0.025f, 0.02f), new Vector2(0, 250), new Vector2(1600, 180));
        t.characterSpacing = 14;
        t.fontStyle = FontStyles.UpperCase;
        t.gameObject.AddComponent<MenuTitleDrips>();

        NewText("Subtitle", root, textFont, textMaterial, 52, subtitle, new Color(0.88f, 0.82f, 0.74f), new Vector2(0, 90), new Vector2(1400, 80));
        var ep = NewText("Epilogue", root, textFont, textMaterial, 32, epilogue, new Color(0.7f, 0.65f, 0.6f, 0.9f), new Vector2(0, 5), new Vector2(1400, 110));
        ep.fontStyle = FontStyles.Italic;

        if (QuestManager.Instance != null)
        {
            int s = Mathf.RoundToInt(QuestManager.Instance.PlayTime);
            string time = s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
            NewText("Time", root, textFont, textMaterial, 30, "Время прохождения:  " + time, new Color(0.62f, 0.56f, 0.5f, 0.85f), new Vector2(0, -80), new Vector2(1000, 50));
        }

        NewButton("ToMenuButton", root, "В главное меню", new Vector2(0, -190), () =>
        {
            var pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null) pause.GoToMainMenu();
        });
        NewButton("QuitButton", root, "Выход", new Vector2(0, -280), Application.Quit);

        NewText("Footer", root, textFont, textMaterial, 26, footer, new Color(0.55f, 0.5f, 0.46f, 0.7f), new Vector2(0, -460), new Vector2(1400, 50));
    }

    void NewButton(string name, Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var plate = NewImage(name, parent);
        plate.sprite = buttonSprite;
        plate.raycastTarget = true;
        plate.rectTransform.sizeDelta = new Vector2(460, 74);
        plate.rectTransform.anchoredPosition = pos;
        var btn = plate.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
        plate.gameObject.AddComponent<ButtonSound>();

        var txt = NewText("Label", plate.transform, textFont, textMaterial, 46, label, Color.white, Vector2.zero, new Vector2(460, 74));
        txt.characterSpacing = 6;

        var fx = plate.gameObject.AddComponent<MenuButtonFx>();
        fx.plate = plate;
        fx.label = txt;
    }

    static Image NewImage(string name, Transform parent)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = Color.black;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, Material mat, float size, string text, Color color, Vector2 pos, Vector2 box)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.fontSize = size;
        t.text = text;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = box;
        t.rectTransform.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
