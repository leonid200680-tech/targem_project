using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public static bool IsDialogueActive { get; private set; } = false; // Чтобы игрок не ходил во время диалога

    [Header("UI Элементы")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public AudioSource voiceSource;

    [Header("Настройки")]
    public float typingSpeed = 0.05f; // Скорость печати текста

    private Queue<DialogueLine> dialogueQueue = new Queue<DialogueLine>();
    private bool isTyping = false;
    private bool cancelTyping = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        // По клику мыши или пробелу переключаем реплики
        if (IsDialogueActive && Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                cancelTyping = true; // Если текст печатается, ускоряем его
            }
            else
            {
                DisplayNextLine(); // Если текст допечатан, показываем следующий
            }
        }
    }

    public void StartDialogue(DialogueLine[] lines)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        IsDialogueActive = true;
        dialoguePanel.SetActive(true);

        dialogueQueue.Clear();
        foreach (DialogueLine line in lines)
        {
            dialogueQueue.Enqueue(line);
        }

        DisplayNextLine();
    }

    private void DisplayNextLine()
    {
        if (dialogueQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine currentLine = dialogueQueue.Dequeue();
        speakerNameText.text = currentLine.speakerName;

        // --- ИЗМЕНЕННЫЙ БЛОК СО ЗВУКОМ ---
        if (currentLine.voiceClip != null)
        {
            // Если в этой реплике ЕСТЬ звук, мы останавливаем старый и включаем новый
            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = currentLine.voiceClip;
                voiceSource.Play();
            }
        }
        // Если файл None, мы НИЧЕГО НЕ ДЕЛАЕМ. Звук с прошлой реплики продолжит играть.
        // ---------------------------------

        StopAllCoroutines();
        StartCoroutine(TypeText(currentLine));
    }

    private IEnumerator TypeText(DialogueLine line)
    {
        isTyping = true;
        cancelTyping = false;
        dialogueText.text = "";

        foreach (char letter in line.text.ToCharArray())
        {
            if (cancelTyping)
            {
                dialogueText.text = line.text; // Мгновенно показываем весь текст
                break;
            }

            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        // Ждем, пока проиграет аудио, прежде чем разрешить клик дальше
        isTyping = false;
    }

    private void EndDialogue()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        IsDialogueActive = false;
        dialoguePanel.SetActive(false);
        voiceSource.Stop();
    }
}