using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Линза для фонарика. Лежит где-то в здании как обычный предмет (PickupItem). Как только у игрока
// есть и линза, и фонарик, линза сама ставится на фонарик: луч бьёт дальше и ярче, становится уже,
// на головке фонарика появляется модель линзы. Внизу экрана - короткая надпись.
public class FlashlightLens : MonoBehaviour
{
    public ItemData lensItem;
    public GameObject lensModel;          // модель линзы на головке фонарика (выключена до установки)
    public float rangeMultiplier = 1.9f;
    public float intensityMultiplier = 1.6f;
    public float angleMultiplier = 0.8f;
    public Font font;

    public bool Installed { get; private set; }

    private Inventory inventory;
    private Flashlight flashlight;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        flashlight = GetComponent<Flashlight>();
        if (lensModel != null) lensModel.SetActive(false);
    }

    void Update()
    {
        if (Installed || inventory == null || flashlight == null || lensItem == null) return;
        if (inventory.Has(lensItem) && flashlight.HasFlashlightInInventory()) Install();
    }

    void Install()
    {
        Installed = true;
        inventory.Remove(lensItem, 1);
        var spot = flashlight.spotLight;
        if (spot != null)
        {
            spot.range *= rangeMultiplier;
            spot.intensity *= intensityMultiplier;
            spot.spotAngle *= angleMultiplier;
            spot.innerSpotAngle *= angleMultiplier;
        }
        if (lensModel != null) lensModel.SetActive(true);
        StartCoroutine(Toast("Линза установлена на фонарик — теперь он светит дальше"));
    }

    IEnumerator Toast(string message)
    {
        var go = new GameObject("Lens Toast", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform, false);
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 34;
        t.alignment = TextAnchor.MiddleCenter;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.22f);
        t.rectTransform.sizeDelta = new Vector2(1400, 60);
        t.text = message;
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);
        Color c = new Color(0.86f, 0.8f, 0.7f, 0f);
        for (float e = 0f; e < 4f; e += Time.deltaTime)
        {
            c.a = Mathf.Clamp01(e * 3f) * Mathf.Clamp01((4f - e) * 1.5f);
            t.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
