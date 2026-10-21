using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("UI и Звук")]
    public Text questText;
    public AudioSource audioSource; // Компонент AudioSource
    public AudioClip questUpdateSound; // Твой звук "дзынь"

    private int currentStep = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Убедимся, что на объекте есть AudioSource
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        SetQuestStep(0);
    }

    public void SetQuestStep(int step)
    {
        currentStep = step;
        string newQuestText = "";

        switch (currentStep)
        {
            case 0: newQuestText = "Найди фонарик."; break;
            case 1: newQuestText = "Проверь телевизор."; break;
            case 2: newQuestText = "Иди в контору."; break;
            case 3: newQuestText = "Квест завершен."; break;
        }

        questText.text = newQuestText;

        // Проигрываем звук и анимацию
        PlayQuestUpdateEffect();
    }

    public int GetCurrentStep() => currentStep;
    public void OnTVWatched() { if (currentStep == 1) SetQuestStep(2); }
    public void OnOfficeReached() { if (currentStep == 2) SetQuestStep(3); }

    void PlayQuestUpdateEffect()
    {
        // 1. Звук
        if (audioSource != null && questUpdateSound != null)
        {
            audioSource.PlayOneShot(questUpdateSound);
        }

        // 2. Анимация (Запускаем корутину, которая сделает текст желтым и большим)
        StopAllCoroutines();
        StartCoroutine(AnimateText());
    }

    IEnumerator AnimateText()
    {
        // Запоминаем изначальный размер и цвет
        Vector3 originalScale = Vector3.one;
        Color originalColor = questText.color;

        // Делаем текст желтым и чуть больше
        questText.color = Color.yellow;
        questText.transform.localScale = originalScale * 1.2f;

        float duration = 0.2f; // Длительность анимации
        float elapsed = 0;

        // Плавно возвращаем как было
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            questText.color = Color.Lerp(Color.yellow, originalColor, t);
            questText.transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);

            yield return null;
        }

        questText.color = originalColor;
        questText.transform.localScale = originalScale;
    }
}