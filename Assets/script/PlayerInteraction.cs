using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3f; // Дальность взаимодействия
    public Text interactionText;        // Ссылка на текст подсказки

    private Camera cam;
    private Inventory playerInventory;

    void Start()
    {
        cam = Camera.main;
        playerInventory = GetComponent<Inventory>(); // Ищем инвентарь на игроке
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        // Пускаем луч из центра экрана
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            // 1. Проверяем, ДВЕРЬ ли это
            InteractableDoor door = hit.collider.GetComponent<InteractableDoor>();
            if (door != null)
            {
                ShowText(door.prompt);
                if (Input.GetKeyDown(KeyCode.E)) door.Interact();
                return;
            }

            // 2. Проверяем, ПРЕДМЕТ ли это
            PickupItem item = hit.collider.GetComponent<PickupItem>();
            if (item != null)
            {
                ShowText(item.prompt);
                if (Input.GetKeyDown(KeyCode.E)) item.Pickup(playerInventory);
                return;
            }
        }

        // Если ни на что не смотрим — прячем текст
        HideText();
    }

    void ShowText(string textToShow)
    {
        if (interactionText != null)
        {
            interactionText.text = textToShow;
            interactionText.gameObject.SetActive(true);
        }
    }

    void HideText()
    {
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }
}