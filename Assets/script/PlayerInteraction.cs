using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки")]
    public float interactDistance = 3f;
    public Text interactionText;

    private Camera cam;
    private Inventory playerInventory;

    void Start()
    {
        // Ищем камеру внутри игрока
        cam = GetComponentInChildren<Camera>();
        playerInventory = GetComponent<Inventory>();

        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (cam == null) return;

        // Если идет диалог, игрок не может взаимодействовать с миром
        if (DialogueManager.IsDialogueActive)
        {
            HideText();
            return;
        }

        if (EvacuationPlan.IsOpen || HideableCloset.IsPlayerHidden)
        {
            HideText();
            return;
        }

        // Луч из центра взгляда (в VR - куда смотрит голова)
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // 1. Проверка на ДВЕРЬ
            // 0. Шкаф-укрытие: и дверцы, и корпус ведут в шкаф
            HideableCloset closetHit = hit.collider.GetComponentInParent<HideableCloset>();
            if (closetHit != null)
            {
                ShowText(closetHit.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) closetHit.Interact();
                return;
            }

            if (hit.collider.TryGetComponent(out InteractableDoor door))
            {
                ShowText(door.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) door.Interact();
                return;
            }

            // 2. Проверка на ПРЕДМЕТ (камни, фонарик и т.д.)
            if (hit.collider.TryGetComponent(out PickupItem item))
            {
                ShowText(playerInventory.CanAdd(item.item) ? item.prompt : "Нет места: G — выбросить предмет в руке");
                if (BotInput.GetKeyDown(KeyCode.E)) item.Pickup(playerInventory);
                return;
            }

            // 3. Проверка на NPC (Наркоман)
            if (hit.collider.TryGetComponent(out NPCDialogueController npc))
            {
                ShowText("Нажмите E, чтобы заговорить");
                if (BotInput.GetKeyDown(KeyCode.E)) npc.Interact();
                return;
            }

            // 4. Проверка на ТЕЛЕФОН (входящий звонок с вибрацией)
            if (hit.collider.TryGetComponent(out PhoneController phone))
            {
                if (phone.IsRinging())
                {
                    ShowText("Нажмите E, чтобы ответить");
                    if (BotInput.GetKeyDown(KeyCode.E)) phone.AnswerPhone();
                }
                else
                {
                    ShowText("Нажмите E, чтобы проверить телефон");
                    if (BotInput.GetKeyDown(KeyCode.E)) phone.StartRinging();
                }
                return;
            }

            // 5. Проверка на ДИАЛОГ (второй телефон с аудиосообщением)
            if (hit.collider.TryGetComponent(out DialogueTrigger trigger))
            {
                ShowText("Нажмите E, чтобы прослушать");
                if (BotInput.GetKeyDown(KeyCode.E)) trigger.TriggerDialogue();
                return;
            }

            // 6. Проверка на ТЕЛЕВИЗОР
            if (hit.collider.TryGetComponent(out InteractableTV tv))
            {
                ShowText(tv.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) tv.Interact();
                return;
            }

            // 7. Проверка на ПЕНТАГРАММУ
            if (hit.collider.TryGetComponent(out Pentagram pentagram))
            {
                ShowText(pentagram.GetPrompt(playerInventory));
                if (BotInput.GetKeyDown(KeyCode.E)) pentagram.Interact(playerInventory);
                return;
            }

            // 8. Проверка на СВЕЧУ
            if (hit.collider.TryGetComponent(out CandleInteract candle))
            {
                ShowText(candle.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) candle.TryLight(playerInventory);
                return;
            }

            // 10. План эвакуации на стене
            if (hit.collider.TryGetComponent(out EvacuationPlan plan))
            {
                ShowText(plan.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) plan.Interact();
                return;
            }

            // 9. Проверка на ШКАФ (Укрытие)
            if (hit.collider.TryGetComponent(out HideableCloset closet))
            {
                ShowText(closet.prompt);
                if (BotInput.GetKeyDown(KeyCode.E)) closet.Interact();
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
            interactionText.text = VRSupport.Prompt(textToShow);
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