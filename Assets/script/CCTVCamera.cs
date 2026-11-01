using UnityEngine;

// Камера видеонаблюдения. Сама ничего не рисует: её включает телевизор (InteractableTV),
// когда игрок листает камеры.
[RequireComponent(typeof(Camera))]
public class CCTVCamera : MonoBehaviour
{
    [Header("Подпись на экране")]
    public string label = "CAM 01";
    public string location = "1 ЭТАЖ";

    [Header("Пугалка")]
    [Tooltip("Дверь, которая сама откроется, пока игрок смотрит в эту камеру")]
    public InteractableDoor doorToOpen;
    public float doorOpenDelay = 2.5f;
    public AudioClip doorCreak;

    [Header("ИК-подсветка (горит, только пока смотрят в эту камеру)")]
    public Light irLight;

    public Camera Cam { get; private set; }

    private bool doorOpened;

    void Awake()
    {
        Cam = GetComponent<Camera>();
        SetLive(false, null);
    }

    // Включает камеру (рендер в текстуру телевизора) вместе с подсветкой
    public void SetLive(bool live, RenderTexture target)
    {
        Cam.targetTexture = live ? target : null;
        Cam.enabled = live;
        if (irLight != null) irLight.enabled = live;
    }

    // Телевизор сообщает, сколько секунд игрок без помех смотрит в эту камеру
    public void OnWatched(float seconds)
    {
        if (doorOpened || doorToOpen == null || seconds < doorOpenDelay) return;

        doorOpened = true;
        doorToOpen.Interact();
        if (doorCreak != null) AudioSource.PlayClipAtPoint(doorCreak, doorToOpen.transform.position, 1f);
    }
}
