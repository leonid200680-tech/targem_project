using UnityEngine;

public class NPCDialogueController : MonoBehaviour
{
    // Состояния NPC
    public enum NPCState { FirstMeet, WaitingForBook, BookReturned, ThirdFloorAmbush }
    public NPCState currentState = NPCState.FirstMeet;

    [Header("Ссылки")]
    public Inventory playerInventory; // Сюда перетащи игрока (у него есть инвентарь)
    public ItemData ritualBook;       // Сюда перетащи предмет "Книга ритуалов"

    [Header("Диалоги (Заполняются в инспекторе)")]
    public DialogueLine[] firstMeetLines;    // Первый диалог
    public DialogueLine[] waitingLines;      // Если игрок пришел без книги
    public DialogueLine[] bookReturnedLines; // Диалог, когда книга в руках
    public DialogueLine[] thirdFloorLines;   // Финальный диалог на 3 этаже

    [Header("Лампа-ориентир у его укрытия")]
    private Light guideLamp;
    private float lampLevel;

    void Start() => PlaceGuideLamp();

    // Красная мигающая лампа у входа к сектанту
    void PlaceGuideLamp()
    {
        // Навмеш не соединяет его комнату с коридором, поэтому ориентир - табличка ближайшего входа на его этаже
        var plates = GameObject.Find("Room Numbers");
        if (plates == null) return;
        Vector3 door = Vector3.zero;
        float best = float.MaxValue;
        foreach (var t in plates.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            Vector3 p = t.transform.position;
            if (p.y < transform.position.y || p.y > transform.position.y + 3f) continue;   // только его этаж
            float d = (p - transform.position).sqrMagnitude;
            if (d < best) { best = d; door = p; }
        }
        if (best == float.MaxValue) return;

        var go = new GameObject("Satanist Guide Lamp");
        go.transform.position = door + Vector3.up * 0.7f;
        guideLamp = go.AddComponent<Light>();
        go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        guideLamp.type = LightType.Point;
        guideLamp.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        guideLamp.range = 7f;
        guideLamp.color = new Color(1f, 0.15f, 0.1f);
        guideLamp.shadows = LightShadows.None;
        lampLevel = 420f;
    }

    void Update()
    {
        if (guideLamp == null) return;
        guideLamp.enabled = currentState != NPCState.ThirdFloorAmbush;
        guideLamp.intensity = lampLevel * (Mathf.PerlinNoise(Time.time * 7f, 0.3f) > 0.42f ? 1f : 0.12f);
    }

    DialogueLine[] WithHint(DialogueLine[] lines, string hint)
    {
        string speaker = firstMeetLines != null && firstMeetLines.Length > 0 ? firstMeetLines[0].speakerName : "";
        var line = new DialogueLine { speakerName = speaker, text = hint };
        if (lines == null) return new[] { line };
        var all = new DialogueLine[lines.Length + 1];
        lines.CopyTo(all, 0);
        all[lines.Length] = line;
        return all;
    }

    string BookHint()
    {
        var qm = QuestManager.Instance;
        if (qm != null)
            foreach (var p in FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.item == ritualBook && p.gameObject.activeSelf) return "Книга там: " + qm.WhereIs(p.transform.position) + ".";
        return "Книгу ищи. Без неё ничего не выйдет.";
    }

    public void Interact()
    {
        switch (currentState)
        {
            case NPCState.FirstMeet:
                StartDialogue(firstMeetLines);
                currentState = NPCState.WaitingForBook; // Меняем состояние
                break;

            case NPCState.WaitingForBook:
                if (playerInventory != null && playerInventory.Has(ritualBook))
                {
                    StartDialogue(bookReturnedLines); // Игрок принес книгу
                    playerInventory.Remove(ritualBook, 1);
                    currentState = NPCState.BookReturned;
                }
                else
                {
                    StartDialogue(WithHint(waitingLines, BookHint())); // Игрок забыл книгу
                }
                break;

            case NPCState.BookReturned:
                var quest = QuestManager.Instance;
                string stones = quest != null ? quest.StonesHint() : null;
                StartDialogue(WithHint(null, stones ?? "Все камни у тебя? Неси их к пентаграмме."));
                break;

            case NPCState.ThirdFloorAmbush:
                StartDialogue(thirdFloorLines);
                break;
        }
    }

    private void StartDialogue(DialogueLine[] lines)
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(lines);
        }
    }

    // Этот метод вызовем, когда игрок поднимется на 3 этаж
    public void StartThirdFloorAmbush()
    {
        currentState = NPCState.ThirdFloorAmbush;
        StartDialogue(thirdFloorLines);
    }
}