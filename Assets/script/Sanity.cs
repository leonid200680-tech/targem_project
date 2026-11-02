using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Рассудок вместо смерти. Когда бабка ловит игрока, он не умирает: темнота, звон в ушах,
// и он приходит в себя у ближайшей лестницы, а бабка уходит в дальний конец здания. Прогресс не теряется,
// но игрок теряет одно деление рассудка (из 3). Чем меньше рассудка, тем выше "пол" страха
// (лампы, голоса, тёмные края экрана) и тем чаще мерещится бабка там, где её нет.
// Если рассудка не осталось и бабка ловит ещё раз - концовка "Петля": игрок просыпается в вагончике,
// снова звонит телефон. От рассудка зависит и финал ритуала (см. EndingTexts).
public class Sanity : MonoBehaviour
{
    public static Sanity Instance { get; private set; }
    // Сколько раз игрока ловили (0 - ни разу)
    public static int Lost => Instance != null ? Instance.lost : 0;
    // Ниже этого уровня шкала страха не опускается
    public static float FearFloor => Instance != null ? Instance.lost * Instance.fearPerLoss : 0f;

    [Header("Рассудок")]
    public int maxSanity = 3;
    [Range(0, 1)] public float fearPerLoss = 0.2f;
    [Tooltip("Сколько секунд после пробуждения бабка не может поймать снова")]
    public float graceTime = 6f;

    [Header("Где приходит в себя: коридор у каждой лестницы")]
    public float[] stairX = { -31.75f, -7.55f, 24.5f };
    public float corridorZ = -8f;
    public float[] floorY = { 3.73f, 7.84f, 11.99f };
    public float minDistanceFromGhost = 10f;

    [Header("Концовка \"Петля\"")]
    public AudioClip momLine;   // "Твоя сестра не отвечает"

    [Header("Оформление")]
    public TMP_FontAsset font;
    public Material fontMaterial;

    [Header("Галлюцинации на последних делениях")]
    public Vector2 apparitionInterval = new Vector2(30f, 70f);
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));

    private int lost;
    private bool busy;
    private Vector3 startPos;
    private Quaternion startRot;
    private PlayerController player;
    private HallucinationManager hallucinations;

    private Canvas canvas;
    private Image black;
    private TextMeshProUGUI sanityText;
    private AudioSource sfx;
    private AudioClip tinnitus, sting;

    private GameObject apparition;
    private float nextApparition;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null) { startPos = player.transform.position; startRot = player.transform.rotation; }
        hallucinations = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        tinnitus = MakeTinnitus();
        sting = MakeSting();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 0f;
        sfx.playOnAwake = false;
        BuildUI();
        apparition = MakeApparition();
        nextApparition = Time.time + Random.Range(apparitionInterval.x, apparitionInterval.y);
    }

    public int Current => Mathf.Max(0, maxSanity - lost);

    // ---------- Поймали ----------

    public void OnCaught(GhostKiller killer)
    {
        if (busy) return;
        StartCoroutine(lost >= maxSanity ? LoopEnding(killer) : WakeUp(killer));
    }

    IEnumerator WakeUp(GhostKiller killer)
    {
        busy = true;
        lost++;

        yield return FadeBlack(0f, 1f, 0.5f);
        sfx.PlayOneShot(tinnitus, 0.6f);

        // Переносим игрока к лестнице подальше от бабки, бабку - в дальний конец
        Transform ghost = killer.transform;
        Vector3 spot = SafeSpot(ghost.position);
        MovePlayer(spot, Quaternion.LookRotation(Vector3.forward));
        var chase = killer.GetComponent<GhostChase>();
        if (chase != null) chase.SendFarFrom(spot);
        killer.Revive(graceTime);

        ShowSanity();
        yield return new WaitForSeconds(1.6f);

        // Приходит в себя: глаза открываются, моргает
        yield return FadeBlack(1f, 0.35f, 0.8f);
        yield return FadeBlack(0.35f, 0.9f, 0.25f);
        yield return FadeBlack(0.9f, 0f, 1.2f);
        yield return new WaitForSeconds(2f);
        yield return FadeText(1f, 0f, 1f);
        busy = false;
    }

    IEnumerator LoopEnding(GhostKiller killer)
    {
        busy = true;
        yield return FadeBlack(0f, 1f, 0.5f);
        sfx.PlayOneShot(tinnitus, 0.8f);

        killer.Revive(float.MaxValue);   // камера встаёт на место; бабка больше не ловит
        killer.gameObject.SetActive(false);
        MovePlayer(startPos, startRot);
        if (player != null) player.enabled = false;
        if (QuestManager.Instance != null && QuestManager.Instance.callPhone != null) QuestManager.Instance.callPhone.StartRinging();

        yield return new WaitForSeconds(2.5f);
        yield return FadeBlack(1f, 0f, 2.5f);
        yield return new WaitForSeconds(2f);
        if (momLine != null)
        {
            sfx.PlayOneShot(momLine, 1f);
            yield return new WaitForSeconds(momLine.length + 0.5f);
        }

        var screen = FindAnyObjectByType<GameCompleteScreen>(FindObjectsInactive.Include);
        if (screen != null)
        {
            ApplyEnding(screen, true);
            screen.Show();
        }
    }

    // Тексты финального экрана: 1 - спасение, 2 - "не одна", 3 - петля
    public static void ApplyEnding(GameCompleteScreen s, bool loop)
    {
        if (loop)
        {
            s.title = "ТЫ ОСТАЛСЯ ЗДЕСЬ";
            s.subtitle = "Рассудок потерян";
            s.epilogue = "Ты открываешь глаза в вагончике. Звонит телефон.\n«Твоя сестра не отвечает...»";
            s.endingLabel = "Концовка 3 из 3  ·  Петля";
        }
        else if (Lost > 0)
        {
            s.subtitle = "Она вернулась... не одна";
            s.epilogue = "Анджела снова дома. Но по ночам она напевает старушечьи песни\nи подолгу смотрит в угол, где никого нет.";
            s.endingLabel = "Концовка 2 из 3  ·  Чужая";
        }
        else
        {
            s.endingLabel = "Концовка 1 из 3  ·  Спасение";
        }
    }

    Vector3 SafeSpot(Vector3 ghostPos)
    {
        float playerY = player != null ? player.transform.position.y : 0f;
        Vector3 best = startPos;
        float bestScore = float.MinValue;
        foreach (float y in floorY)
            foreach (float x in stairX)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, y, corridorZ), out var hit, 2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - y) > 0.6f) continue;
                float fromGhost = Vector3.Distance(hit.position, ghostPos);
                if (fromGhost < minDistanceFromGhost) continue;
                // свой этаж важнее всего, дальше - подальше от бабки
                float score = fromGhost - Mathf.Abs(y - playerY) * 10f;
                if (score > bestScore) { bestScore = score; best = hit.position; }
            }
        return best;
    }

    void MovePlayer(Vector3 pos, Quaternion rot)
    {
        if (player == null) return;
        HideableCloset.IsPlayerHidden = false;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, rot);
        if (cc != null) cc.enabled = true;
    }

    // ---------- Галлюцинации ----------

    void Update()
    {
        if (busy || player == null || !player.enabled || Current > 1 || apparition == null) return;
        if (Time.time < nextApparition) return;
        float k = Current == 0 ? 0.5f : 1f;   // на нуле - вдвое чаще
        nextApparition = Time.time + Random.Range(apparitionInterval.x, apparitionInterval.y) * k;

        var ghost = hallucinations != null ? hallucinations.realGhost : null;
        bool chased = ghost != null && ghost.activeInHierarchy && Vector3.Distance(ghost.transform.position, player.transform.position) < 15f;
        if (chased || DialogueManager.IsDialogueActive || !buildingZone.Contains(player.transform.position)) return;
        StartCoroutine(ShowApparition());
    }

    IEnumerator ShowApparition()
    {
        var cam = Camera.main != null ? Camera.main.transform : player.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = cam.position + Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * fwd * Random.Range(7f, 12f);
            if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - player.transform.position.y) > 1f) continue;
            if (Physics.Linecast(cam.position, hit.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;

            Vector3 look = cam.position - hit.position; look.y = 0f;
            apparition.transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation(look));
            apparition.SetActive(true);
            sfx.PlayOneShot(sting, 0.45f);
            yield return new WaitForSeconds(Random.Range(0.25f, 0.45f));
            apparition.SetActive(false);
            yield break;
        }
    }

    // Копия бабки без поведения: только модель
    GameObject MakeApparition()
    {
        var src = hallucinations != null ? hallucinations.realGhost : null;
        if (src == null) return null;
        bool was = src.activeSelf;
        src.SetActive(false);
        var go = Instantiate(src);
        src.SetActive(was);
        go.name = "Sanity Apparition";
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(b);
        foreach (var a in go.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var s in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(s);
        return go;
    }

    // ---------- Экран ----------

    void BuildUI()
    {
        var go = new GameObject("Sanity UI", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        black.transform.SetParent(go.transform, false);
        var rt = black.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        sanityText = new GameObject("Sanity Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        sanityText.transform.SetParent(go.transform, false);
        if (font != null) sanityText.font = font;
        if (fontMaterial != null) sanityText.fontSharedMaterial = fontMaterial;
        sanityText.fontSize = 72;
        sanityText.alignment = TextAlignmentOptions.Center;
        sanityText.color = new Color(0.62f, 0.05f, 0.04f, 0f);
        sanityText.raycastTarget = false;
        sanityText.rectTransform.sizeDelta = new Vector2(1200, 160);
        sanityText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
    }

    void ShowSanity()
    {
        string dots = "";
        for (int i = 0; i < maxSanity; i++) dots += i < Current ? "●" : "○";
        string line = Current > 0 ? "Ты теряешь рассудок" : "Рассудок на исходе... больше нельзя попадаться";
        sanityText.text = $"<size=60%>{line}</size>\n<cspace=18>{dots}</cspace>";
        sanityText.color = new Color(0.62f, 0.05f, 0.04f, 1f);
    }

    IEnumerator FadeBlack(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        black.color = new Color(0f, 0f, 0f, to);
    }

    IEnumerator FadeText(float from, float to, float time)
    {
        Color c = sanityText.color;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            sanityText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        sanityText.color = new Color(c.r, c.g, c.b, to);
    }

    // ---------- Звук ----------

    // Звон в ушах: высокий тон, плавающий и затихающий, с шумом
    static AudioClip MakeTinnitus()
    {
        const int rate = 44100;
        int n = rate * 4;
        var d = new float[n];
        var rnd = new System.Random(21);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            ph += (3900f + 40f * Mathf.Sin(t * 3f)) / rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.08f;
            float env = Mathf.Clamp01(t * 8f) * Mathf.Exp(-t * 0.7f);
            d[i] = (Mathf.Sin(ph * 2f * Mathf.PI) * 0.25f + lp * 0.5f) * env;
        }
        var c = AudioClip.Create("Tinnitus", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // Глухой удар для мелькнувшей бабки
    static AudioClip MakeSting()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.2f);
        var d = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            ph += Mathf.Lerp(70f, 35f, Mathf.Clamp01(t)) / rate;
            d[i] = Mathf.Sin(ph * 2f * Mathf.PI) * Mathf.Exp(-t * 3f) * Mathf.Clamp01(t * 80f) * 0.9f;
        }
        var c = AudioClip.Create("Sanity Sting", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
