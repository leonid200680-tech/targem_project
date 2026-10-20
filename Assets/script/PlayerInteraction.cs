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
        cam = Camera.main;
        playerInventory = GetComponent<Inventory>();
        if (interactionText != null) interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            if (hit.collider.TryGetComponent(out InteractableDoor door))
            {
                ShowText(door.prompt);
                if (Input.GetKeyDown(KeyCode.E)) door.Interact();
                return;
            }
            if (hit.collider.TryGetComponent(out PickupItem item))
            {
                ShowText(item.prompt);
                if (Input.GetKeyDown(KeyCode.E)) item.Pickup(playerInventory);
                return;
            }
            if (hit.collider.TryGetComponent(out InteractableTV tv))
            {
                ShowText(tv.prompt);
                if (Input.GetKeyDown(KeyCode.E)) tv.Interact();
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