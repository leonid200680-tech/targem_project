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
                    StartDialogue(waitingLines); // Игрок забыл книгу
                }
                break;

            case NPCState.BookReturned:
                StartDialogue(waitingLines); // "Ну что, нашел камни?"
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