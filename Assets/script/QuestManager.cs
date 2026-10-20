using UnityEngine;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public Text questText;

    // Текущий шаг квеста (0 - взять фонарик, 1 - посмотреть телек, 2 - идти в контору, 3 - конец)
    private int currentStep = 0;

    void Awake()
    {
        // Делаем так, чтобы к менеджеру можно было обращаться из любого скрипта
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Начинаем с первого квеста
        SetQuestStep(0);
    }

    public void SetQuestStep(int step)
    {
        currentStep = step;

        // Меняем текст на экране в зависимости от шага
        switch (currentStep)
        {
            case 0:
                questText.text = "Найди фонарик.";
                break;
            case 1:
                questText.text = "Проверь телевизор.";
                break;
            case 2:
                questText.text = "Иди в контору.";
                break;
            case 3:
                questText.text = "Квест завершен.";
                break;
        }
        Debug.Log("Квест обновлен. Шаг: " + currentStep);
    }

    public int GetCurrentStep()
    {
        return currentStep;
    }

    // Этот метод вызовет телевизор
    public void OnTVWatched()
    {
        if (currentStep == 1)
        {
            SetQuestStep(2); // Переходим к походу в контору
        }
    }

    // Этот метод вызовет триггер конторы
    public void OnOfficeReached()
    {
        if (currentStep == 2)
        {
            SetQuestStep(3); // Квест выполнен
        }
    }
}