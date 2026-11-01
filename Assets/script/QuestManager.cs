using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Цепочка квестов. Большинство шагов засчитываются сами: менеджер следит за телефоном,
// инвентарём, положением игрока, наркоманом, пентаграммой и свечами.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public enum Step
    {
        AnswerPhone,   // 1) Возьми телефон, ответь на звонок
        WatchCameras,  // 2) Телек барахлит: включи и пролистай камеры
        TakeItems,     // 3) Забери фонарик и зажигалку
        ThirdFloor,    // 4) Посмотри, что творится на 3 этаже
        SecondFloor,   // 5) Обойди 2 этаж (найди наркомана)
        BringBook,     // 6) Найди книгу и принеси
        PlaceStones,   // 7) Найди камни и выложи для ритуала
        LightCandles,  // 8) Зажги свечи
        Done
    }

    [Header("UI и Звук")]
    public Text questText;
    public AudioSource audioSource; // Компонент AudioSource
    public AudioClip questUpdateSound; // Твой звук "дзынь"

    [Header("Участники квестов")]
    public PhoneController callPhone;          // телефон, на который звонит мама
    public InteractableTV tv;
    public NPCDialogueController addict;       // наркоман-сатанист на 2 этаже
    public Pentagram pentagram;
    public float ringDelay = 2f;               // пауза после того, как игрок "проснулся"

    [Header("Зона 3 этажа (мировые координаты)")]
    public Bounds thirdFloorZone = new Bounds(new Vector3(-3.5f, 13.5f, -5.3f), new Vector3(62f, 3.4f, 22f));

    private Step currentStep = Step.AnswerPhone;
    private PlayerController player;
    private Inventory inventory;
    private CandleInteract[] candles;
    private Color baseTextColor = Color.white;
    private bool phoneWasRinging;
    private bool phoneAnswered;
    private bool ringStarted;
    private float playerReadyTime = -1f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Убедимся, что на объекте есть AudioSource
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (tv == null) tv = FindAnyObjectByType<InteractableTV>();
        if (addict == null) addict = FindAnyObjectByType<NPCDialogueController>();
        if (pentagram == null) pentagram = FindAnyObjectByType<Pentagram>();
        candles = FindObjectsByType<CandleInteract>(FindObjectsInactive.Include);
        if (questText != null) baseTextColor = questText.color;

        SetStep(Step.AnswerPhone, false);
    }

    void Update()
    {
        // Игрок появляется только после "Начать игру"
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            inventory = player.GetComponent<Inventory>();
        }

        // Пока идёт диалог, шаги не засчитываем: пусть игрок дослушает
        if (DialogueManager.IsDialogueActive) return;

        // Несколько шагов могут оказаться выполненными сразу (например, предметы уже подобраны)
        for (int guard = 0; guard < 8 && IsCurrentStepComplete(); guard++)
            SetStep(currentStep + 1);

        UpdateProgressText();
    }

    bool IsCurrentStepComplete()
    {
        switch (currentStep)
        {
            case Step.AnswerPhone: return UpdatePhone();
            case Step.WatchCameras: return tv != null && tv.AllCamerasSeen;
            case Step.TakeItems: return HasItem(ItemType.Flashlight) && HasItem(ItemType.Lighter);
            case Step.ThirdFloor: return thirdFloorZone.Contains(player.transform.position);
            case Step.SecondFloor: return addict == null || addict.currentState != NPCDialogueController.NPCState.FirstMeet;
            case Step.BringBook:
                return addict == null || addict.currentState == NPCDialogueController.NPCState.BookReturned
                    || addict.currentState == NPCDialogueController.NPCState.ThirdFloorAmbush;
            case Step.PlaceStones: return StonesPlaced() >= StonesTotal();
            case Step.LightCandles: return CandlesLit() >= candles.Length;
        }
        return false;
    }

    // Телефон начинает звонить, когда у игрока появилось управление (после заставки "моргания")
    bool UpdatePhone()
    {
        if (callPhone == null) return true;

        if (!ringStarted && player.enabled)
        {
            if (playerReadyTime < 0f) playerReadyTime = Time.time;
            if (Time.time - playerReadyTime >= ringDelay)
            {
                ringStarted = true;
                callPhone.StartRinging();
            }
        }

        bool ringing = callPhone.IsRinging();
        if (phoneWasRinging && !ringing) phoneAnswered = true;
        phoneWasRinging = ringing;
        return phoneAnswered;
    }

    bool HasItem(ItemType type)
    {
        if (inventory == null) return false;
        foreach (var slot in inventory.slots)
            if (slot.item != null && slot.item.type == type) return true;
        return false;
    }

    int StonesTotal() => pentagram != null ? pentagram.stonesOnAltar.Length : 0;

    int StonesPlaced()
    {
        if (pentagram == null) return 0;
        int n = 0;
        foreach (var stone in pentagram.stonesOnAltar)
            if (stone != null && stone.activeSelf) n++;
        return n;
    }

    int CandlesLit()
    {
        int n = 0;
        foreach (var c in candles)
            if (c != null && c.isLit) n++;
        return n;
    }

    string StepText(Step step)
    {
        switch (step)
        {
            case Step.AnswerPhone: return "Возьми телефон и ответь на звонок.";
            case Step.WatchCameras: return "Телевизор барахлит. Включи его и проверь камеры";
            case Step.TakeItems: return "Забери фонарик и зажигалку";
            case Step.ThirdFloor: return "Посмотри, что творится на 3 этаже здания.";
            case Step.SecondFloor: return "Обойди 2 этаж. Прислушайся...";
            case Step.BringBook: return "Найди книгу и принеси её наркоману.";
            case Step.PlaceStones: return "Найди камни и выложи их для ритуала";
            case Step.LightCandles: return "Зажги свечи";
            case Step.Done: return "Ритуал завершён.";
        }
        return "";
    }

    string ProgressSuffix()
    {
        switch (currentStep)
        {
            case Step.WatchCameras: return tv != null ? $" ({tv.CamerasSeen}/{tv.CameraCount})." : ".";
            case Step.TakeItems: return $" ({(HasItem(ItemType.Flashlight) ? 1 : 0) + (HasItem(ItemType.Lighter) ? 1 : 0)}/2).";
            case Step.PlaceStones: return $" ({StonesPlaced()}/{StonesTotal()}).";
            case Step.LightCandles: return $" ({CandlesLit()}/{(candles != null ? candles.Length : 0)}).";
        }
        return "";
    }

    void UpdateProgressText()
    {
        if (questText == null) return;
        string text = StepText(currentStep) + ProgressSuffix();
        if (questText.text != text) questText.text = text;
    }

    void SetStep(Step step, bool withEffect = true)
    {
        currentStep = step;
        if (questText != null) questText.text = StepText(step) + ProgressSuffix();

        // Проигрываем звук и анимацию
        if (withEffect) PlayQuestUpdateEffect();
    }

    // Совместимость со старыми вызовами (номер шага = Step)
    public void SetQuestStep(int step) => SetStep((Step)Mathf.Clamp(step, 0, (int)Step.Done));

    public int GetCurrentStep() => (int)currentStep;
    public Step CurrentStep => currentStep;

    public void OnTVWatched() { }      // телевизор засчитывается, когда пролистаны все камеры
    public void OnOfficeReached() { }  // контора больше не отдельный шаг

    // Вызывает пентаграмма, когда разложены камни и зажжены свечи
    public void OnRitualComplete()
    {
        if (currentStep < Step.Done) SetStep(Step.Done);
    }

    void PlayQuestUpdateEffect()
    {
        // 1. Звук
        if (audioSource != null && questUpdateSound != null)
        {
            audioSource.PlayOneShot(questUpdateSound);
        }

        // 2. Анимация (Запускаем корутину, которая сделает текст желтым и большим)
        if (questText == null) return;
        StopAllCoroutines();
        StartCoroutine(AnimateText());
    }

    IEnumerator AnimateText()
    {
        Vector3 originalScale = Vector3.one;

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

            questText.color = Color.Lerp(Color.yellow, baseTextColor, t);
            questText.transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);

            yield return null;
        }

        questText.color = baseTextColor;
        questText.transform.localScale = originalScale;
    }
}
