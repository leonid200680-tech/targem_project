using UnityEngine;
using UnityEngine.UI;

// План эвакуации на стене. Картинка этажа (стены, лестницы, выходы) рисуется заранее редактором
// (Tools > Level > Place Evacuation Plans). По E план открывается на весь экран: с номерами кабинетов
// и отметкой "Вы здесь"; E закрывает.
public class EvacuationPlan : MonoBehaviour
{
    public string prompt = "Нажмите E, чтобы посмотреть план";
    public Texture2D planTexture;
    public int floor = 1;
    [Tooltip("Где на плане стоит этот план (0..1 по ширине и высоте картинки этажа)")]
    public Vector2 youAreHere;
    public Vector2[] roomPositions;   // 0..1
    public string[] roomNames;
    [Tooltip("Показывать карту повёрнутой на 180°: так, как стоит смотрящий на план")]
    public bool rotate180;

    public static bool IsOpen { get; private set; }

    private static GameObject view;
    private static RawImage viewImage;
    private static RectTransform marksRoot;
    private static Text viewTitle;
    private static EvacuationPlan current;
    private PlayerController player;
    private int openedFrame = -1;

    private static int closedFrame = -1;

    public void Interact()
    {
        // та же E, что закрыла план, не должна сразу открыть его снова
        if (IsOpen || Time.frameCount == openedFrame || Time.frameCount == closedFrame) return;
        Open();
    }

    void Open()
    {
        BuildView();
        current = this;
        IsOpen = true;
        openedFrame = Time.frameCount;
        player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.enabled = false;

        viewTitle.text = $"ПЛАН ЭВАКУАЦИИ  •  {floor} ЭТАЖ";
        viewImage.texture = planTexture;
        viewImage.uvRect = rotate180 ? new Rect(1f, 1f, -1f, -1f) : new Rect(0f, 0f, 1f, 1f);

        foreach (Transform t in marksRoot) Destroy(t.gameObject);
        if (roomPositions != null)
            for (int i = 0; i < roomPositions.Length && i < roomNames.Length; i++)
                AddMark(roomNames[i], Oriented(roomPositions[i]), 22, new Color(0.15f, 0.15f, 0.17f), false);
        AddMark("● ВЫ ЗДЕСЬ", Oriented(youAreHere), 26, new Color(0.85f, 0.05f, 0.03f), true);

        view.SetActive(true);
        // Карта вписывается в область листа с сохранением пропорций картинки
        Canvas.ForceUpdateCanvases();
        var area = ((RectTransform)viewImage.transform.parent).rect;
        float aspect = planTexture != null ? planTexture.width / (float)planTexture.height : 3f;
        float w = Mathf.Min(area.width, area.height * aspect);
        var rt = viewImage.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, w / aspect);
        rt.anchoredPosition = Vector2.zero;
    }

    Vector2 Oriented(Vector2 uv) => rotate180 ? Vector2.one - uv : uv;

    void Update()
    {
        if (current != this || !IsOpen) return;
        if (Time.frameCount != openedFrame && BotInput.GetKeyDown(KeyCode.E)) Close();
    }

    void Close()
    {
        IsOpen = false;
        current = null;
        closedFrame = Time.frameCount;
        if (view != null) view.SetActive(false);
        if (player != null) player.enabled = true;
    }

    public static bool CloseOpen()
    {
        if (!IsOpen || current == null) return false;
        current.Close();
        return true;
    }

    void OnDisable()
    {
        if (current == this) Close();
    }

    static void AddMark(string text, Vector2 pos, int size, Color color, bool bold)
    {
        var t = new GameObject(text, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.rectTransform.SetParent(marksRoot, false);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = pos;
        t.rectTransform.sizeDelta = new Vector2(220, 40);
        // у отметки "вы здесь" точка в начале строки - она и должна стоять на месте
        if (bold) t.rectTransform.pivot = new Vector2(0.04f, 0.5f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = bold ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
    }

    // Экран просмотра: лист плана на тёмном фоне
    static void BuildView()
    {
        if (view != null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        view = new GameObject("Evacuation Plan View", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(view);
        var canvas = view.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = view.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dim = NewImage<Image>("Dim", view.transform);
        Stretch(dim.rectTransform, 0f);
        dim.color = new Color(0f, 0f, 0f, 0.85f);

        var sheet = NewImage<Image>("Sheet", view.transform);
        Stretch(sheet.rectTransform, 70f);
        sheet.color = new Color(0.93f, 0.91f, 0.84f);

        viewTitle = new GameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        viewTitle.rectTransform.SetParent(sheet.rectTransform, false);
        viewTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        viewTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        viewTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        viewTitle.rectTransform.sizeDelta = new Vector2(0f, 90f);
        viewTitle.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        viewTitle.font = font;
        viewTitle.fontSize = 44;
        viewTitle.fontStyle = FontStyle.Bold;
        viewTitle.alignment = TextAnchor.MiddleCenter;
        viewTitle.color = new Color(0.1f, 0.45f, 0.18f);

        var area = new GameObject("MapArea", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(sheet.rectTransform, false);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(40f, 80f);
        area.offsetMax = new Vector2(-40f, -110f);

        viewImage = NewImage<RawImage>("Map", area);   // размер задаётся при открытии под пропорции картинки

        marksRoot = new GameObject("Marks", typeof(RectTransform)).GetComponent<RectTransform>();
        marksRoot.SetParent(viewImage.rectTransform, false);
        Stretch(marksRoot, 0f);

        var legend = new GameObject("Legend", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        legend.rectTransform.SetParent(sheet.rectTransform, false);
        legend.rectTransform.anchorMin = Vector2.zero;
        legend.rectTransform.anchorMax = new Vector2(1f, 0f);
        legend.rectTransform.pivot = new Vector2(0.5f, 0f);
        legend.rectTransform.sizeDelta = new Vector2(-80f, 60f);
        legend.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        legend.font = font;
        legend.fontSize = 24;
        legend.alignment = TextAnchor.MiddleLeft;
        legend.color = new Color(0.2f, 0.2f, 0.2f);
        legend.text = "<color=#2e9a3c>■</color> лестницы и выходы        <color=#d10e08>●</color> вы здесь        E / Esc — закрыть";

        view.SetActive(false);
    }

    static T NewImage<T>(string name, Transform parent) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        g.rectTransform.SetParent(parent, false);
        g.raycastTarget = false;
        return g;
    }

    static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }
}
