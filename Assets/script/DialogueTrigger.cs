using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Данные диалога")]
    public DialogueLine[] dialogueLines;

    // Вызывай этот метод, когда игрок нажимает E, или когда начинается триггер
    public void TriggerDialogue()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogueLines);
        }
    }
}