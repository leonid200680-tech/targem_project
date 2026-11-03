using UnityEngine;

// Видео-катсцена на весь экран при любом разрешении: картинка 16:9 покрывает экран целиком
// (лишнее обрезается по краям), чтобы сверху/снизу не просвечивала игра.
[RequireComponent(typeof(RectTransform))]
public class FullscreenVideoFit : MonoBehaviour
{
    public float aspect = 16f / 9f;

    void LateUpdate()
    {
        var rt = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var root = (RectTransform)canvas.rootCanvas.transform;

        Vector2 screen = root.rect.size;
        Vector2 size = screen.x / screen.y > aspect
            ? new Vector2(screen.x, screen.x / aspect)
            : new Vector2(screen.y * aspect, screen.y);
        size += Vector2.one * 4f;   // запас в пару пикселей от щелей на краях

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size / rt.lossyScale.x * root.lossyScale.x;
        rt.position = root.position;
    }
}
