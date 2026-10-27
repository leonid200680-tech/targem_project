using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3f;
    public Text interactionText;
    private Camera cam;
    private Inventory playerInventory;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        playerInventory = GetComponent<Inventory>();
        if (interactionText != null) interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // Проверка на ДВЕРЬ
            if (hit.collider.TryGetComponent(out InteractableDoor door))
            {
                ShowText(door.prompt);
                if (Input.GetKeyDown(KeyCode.E)) door.Interact();
                return;
            }
            // Проверка на ПРЕДМЕТ (камни, фонарик и т.д.)
            if (hit.collider.TryGetComponent(out PickupItem item))
            {
                ShowText(item.prompt);
                if (Input.GetKeyDown(KeyCode.E)) item.Pickup(playerInventory);
                return;
            }
            // Проверка на ПЕНТАГРАММУ
            if (hit.collider.TryGetComponent(out Pentagram pentagram))
            {
                ShowText("Нажмите E, чтобы положить камень");
                if (Input.GetKeyDown(KeyCode.E)) pentagram.PlaceStone(playerInventory);
                return;
            }
        }
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
        if (interactionText != null) interactionText.gameObject.SetActive(false);
    }
}