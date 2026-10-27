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
        // Если идет диалог, игрок не может взаимодействовать с миром
        if (DialogueManager.IsDialogueActive)
        {
            HideText();
            return;
        }

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // 1. Проверка на ДВЕРЬ
            if (hit.collider.TryGetComponent(out InteractableDoor door))
            {
                ShowText(door.prompt);
                if (Input.GetKeyDown(KeyCode.E)) door.Interact();
                return;
            }

            // 2. Проверка на ПРЕДМЕТ (камни, фонарик и т.д.)
            if (hit.collider.TryGetComponent(out PickupItem item))
            {
                ShowText(item.prompt);
                if (Input.GetKeyDown(KeyCode.E)) item.Pickup(playerInventory);
                return;
            }

            // 3. Проверка на NPC (Наркоман)
            if (hit.collider.TryGetComponent(out NPCDialogueController npc))
            {
                ShowText("Нажмите E, чтобы заговорить");
                if (Input.GetKeyDown(KeyCode.E)) npc.Interact();
                return;
            }

            // 4. Проверка на ТЕЛЕФОН (входящий звонок с вибрацией)
            if (hit.collider.TryGetComponent(out PhoneController phone))
            {
                if (phone.IsRinging())
                {
                    ShowText("Нажмите E, чтобы ответить");
                    if (Input.GetKeyDown(KeyCode.E)) phone.AnswerPhone();
                }
                else
                {
                    ShowText("Нажмите E, чтобы проверить телефон");
                    if (Input.GetKeyDown(KeyCode.E)) phone.StartRinging();
                }
                return;
            }

            // 5. Проверка на ДИАЛОГ (второй телефон с аудиосообщением)
            if (hit.collider.TryGetComponent(out DialogueTrigger trigger))
            {
                ShowText("Нажмите E, чтобы прослушать");
                if (Input.GetKeyDown(KeyCode.E)) trigger.TriggerDialogue();
                return;
            }

            // 6. Проверка на ПЕНТАГРАММУ
            if (hit.collider.TryGetComponent(out Pentagram pentagram))
            {
                ShowText("Нажмите E, чтобы положить камень");
                if (Input.GetKeyDown(KeyCode.E)) pentagram.PlaceStone(playerInventory);
                return;
            }
        }

        // Если луч ни во что не попал или не нашел нужных компонентов
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
        {
            interactionText.gameObject.SetActive(false);
        }
    }
}