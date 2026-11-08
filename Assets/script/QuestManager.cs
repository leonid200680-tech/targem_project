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
        PlaceDoll,     // 9) Положи куклу наркомана в центр пентаграммы
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
    private readonly System.Collections.Generic.List<GameObject> hiddenStones = new System.Collections.Generic.List<GameObject>();
    private PickupItem bookPickup;
    private bool npcHushDone;
    private float nextFalseHush;
    private readonly System.Collections.Generic.List<PickupItem> stonePickups = new System.Collections.Generic.List<PickupItem>();
    private readonly System.Collections.Generic.List<(Vector3 pos, string number)> roomPlates = new System.Collections.Generic.List<(Vector3, string)>();

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
        if (questText != null)
        {
            baseTextColor = questText.color;
            questText.verticalOverflow = VerticalWrapMode.Overflow;   // длинный текст не должен обрезаться
        }

        // Камни для ритуала появляются только когда книга отдана сектанту
        foreach (var p in FindObjectsByType<PickupItem>(FindObjectsInactive.Include))
        {
            if (p.item != null && addict != null && p.item == addict.ritualBook) bookPickup = p;
            if (p.item == null || p.item.type != ItemType.Stone) continue;
            if (pentagram != null && p.transform.IsChildOf(pentagram.transform)) continue;
            var root = p.transform;
            while (root.parent != null) root = root.parent;
            if (!root.gameObject.activeSelf && !hiddenStones.Contains(root.gameObject)) continue;
            stonePickups.Add(p);
            if (!root.gameObject.activeSelf) continue;
            root.gameObject.SetActive(false);
            hiddenStones.Add(root.gameObject);
        }

        // Номера кабинетов запоминаем сразу: пугалка потом подменяет цифры на табличках
        var plates = GameObject.Find("Room Numbers");
        if (plates != null)
            foreach (var t in plates.GetComponentsInChildren<TMPro.TMP_Text>(true))
                roomPlates.Add((t.transform.position, t.text));

        SetStep(Step.AnswerPhone, false);
    }

    static int FloorOf(float y) => y < 5.8f ? 1 : y < 9.9f ? 2 : 3;

    // Словами, где лежит предмет: этаж и ближайшая табличка кабинета на этом этаже
    public string WhereIs(Vector3 p)
    {
        if (p.z > 5.2f || p.z < -15.8f || p.x < -34.5f || p.x > 26.8f) return "снаружи, у здания";
        int floor = FloorOf(p.y);
        string room = RoomNear(p);
        return room != null ? $"{floor} этаж, у кабинета {room}" : $"{floor} этаж";
    }

    string RoomNear(Vector3 p)
    {
        int floor = FloorOf(p.y);
        string room = null;
        float best = float.MaxValue;
        foreach (var (pos, number) in roomPlates)
        {
            if (FloorOf(pos.y) != floor) continue;
            float d = (pos - p).sqrMagnitude;
            if (d < best) { best = d; room = number; }
        }
        return room;
    }

    bool StoneLeft(PickupItem p) => p != null && p.gameObject.activeSelf;

    // Подсказка сектанта: только про ненайденные камни. null - все найдены
    public string StonesHint()
    {
        var places = new System.Collections.Generic.List<string>();
        foreach (var p in stonePickups)
            if (StoneLeft(p)) places.Add(WhereIs(p.transform.position));
        return places.Count == 0 ? null : "Камни ещё лежат там: " + string.Join("; ", System.Linq.Enumerable.Distinct(places)) + ".";
    }

    string StonesByFloor()
    {
        var text = new System.Text.StringBuilder();
        for (int f = 1; f <= 3; f++)
        {
            var rooms = new System.Collections.Generic.List<string>();
            foreach (var p in stonePickups)
                if (StoneLeft(p) && FloorOf(p.transform.position.y) == f)
                {
                    string room = RoomNear(p.transform.position);
                    if (room != null && !rooms.Contains(room)) rooms.Add(room);
                }
            if (rooms.Count > 0) text.Append($"\n{f} этаж, у кабинетов: {string.Join(", ", rooms)}");
        }
        return text.Length > 0 ? text.ToString() : "\nВсе камни найдены: неси их к пентаграмме";
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

        // Время прохождения считаем с момента, когда игрок впервые получил управление
        if (playerReadyTime < 0f && player.enabled) playerReadyTime = Time.time;

        // Пока идёт диалог, шаги не засчитываем: пусть игрок дослушает
        if (DialogueManager.IsDialogueActive) return;

        // Несколько шагов могут оказаться выполненными сразу (например, предметы уже подобраны)
        for (int guard = 0; guard < 8 && IsCurrentStepComplete(); guard++)
            SetStep(currentStep + 1);

        UpdateSilences();
        UpdateProgressText();
    }

    // Ложная тишина и затишье перед встречей с сектантом
    void UpdateSilences()
    {
        if (currentStep == Step.SecondFloor && !npcHushDone && addict != null
            && Mathf.Abs(player.transform.position.y - addict.transform.position.y) < 2.5f   // только на его этаже
            && Vector3.Distance(player.transform.position, addict.transform.position) < 12f)
        {
            npcHushDone = true;
            OutdoorSound.HushFor(5f);
        }
        if (currentStep < Step.ThirdFloor || currentStep >= Step.Done) return;
        if (nextFalseHush <= 0f) nextFalseHush = Time.time + Random.Range(120f, 240f);
        else if (Time.time >= nextFalseHush)
        {
            nextFalseHush = 0f;
            OutdoorSound.HushFor(3.5f);
        }
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
            case Step.PlaceDoll: return pentagram == null || !pentagram.NeedsDoll || pentagram.DollPlaced;
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
            case Step.AnswerPhone: return "Где-то рядом звонит телефон. Найди его и ответь.";
            case Step.WatchCameras: return "Телевизор барахлит. Включи его и проверь камеры";
            case Step.TakeItems: return "Забери фонарик и зажигалку";
            case Step.ThirdFloor: return "Посмотри, что творится на 3 этаже здания.";
            case Step.SecondFloor: return "Спустись на 2 этаж. Прислушайся...";
            case Step.BringBook: return "Найди книгу и принеси её сектанту.";
            case Step.PlaceStones: return "Найди камни и выложи их для ритуала";
            case Step.LightCandles: return "Зажги свечи";
            case Step.PlaceDoll: return HasItem(ItemType.Doll)
                ? "Положи куклу в центр пентаграммы."
                : "Забери куклу сектанта и положи её в центр пентаграммы.";
            case Step.Done: return "Ритуал завершён.";
        }
        return "";
    }

    string ProgressSuffix()
    {
        switch (currentStep)
        {
            case Step.WatchCameras: return tv != null ? $" ({tv.CamerasSeen}/{tv.CameraCount})." : ".";
            case Step.BringBook:
                if (addict != null && HasItem(ItemType.Book)) return "\nКнига у тебя: неси её сектанту";
                return bookPickup != null ? "\nКнига: " + WhereIs(bookPickup.transform.position) : "";
            case Step.TakeItems: return $" ({(HasItem(ItemType.Flashlight) ? 1 : 0) + (HasItem(ItemType.Lighter) ? 1 : 0)}/2).";
            case Step.PlaceStones: return $" ({StonesPlaced()}/{StonesTotal()})." + StonesByFloor();
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
        if (withEffect && (step == Step.SecondFloor || step == Step.PlaceDoll)) OutdoorSound.HushFor(4f);   // поднялся на 3 этаж, зажёг последнюю свечу
        if (step >= Step.PlaceStones)
        {
            foreach (var s in hiddenStones) if (s != null) s.SetActive(true);
            hiddenStones.Clear();
        }
        if (questText != null) questText.text = StepText(step) + ProgressSuffix();

        // Проигрываем звук и анимацию
        if (withEffect) PlayQuestUpdateEffect();
    }

    // Совместимость со старыми вызовами (номер шага = Step)
    public void SetQuestStep(int step) => SetStep((Step)Mathf.Clamp(step, 0, (int)Step.Done));

    public int GetCurrentStep() => (int)currentStep;
    // Сколько секунд прошло с момента, когда игрок получил управление
    public float PlayTime => playerReadyTime < 0f ? 0f : Time.time - playerReadyTime;
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
