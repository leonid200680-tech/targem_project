using System.Collections.Generic;
using UnityEngine;

// Крыса для фона: перебегает вдоль стены по заданному пути, иногда замирает,
// пищит и шуршит лапками. Если игрок рядом - бежит быстрее. Добежав до конца, прячется (исчезает).
// Модель - Rat by Quaternius (poly.pizza, CC0): бег со скоростью движения, на остановках - Idle.
public class Rat : MonoBehaviour
{
    public Animator animator;
    public AudioSource audioSource;

    [Header("Бег")]
    public float speed = 3.2f;
    public float fleeSpeed = 5f;
    public float fleeDistance = 3f;
    public float turnSpeed = 720f;
    [Range(0, 1)] public float pauseChance = 0.35f;
    [Tooltip("Скорость анимации бега на 1 м/с движения")]
    public float animPerMeter = 1.2f;

    [HideInInspector] public AudioClip[] squeaks;
    [HideInInspector] public AudioClip patter;

    private readonly List<Vector3> path = new List<Vector3>();
    private int corner;
    private float pauseUntil;
    private float nextPauseCheck;
    private float nextSqueak;
    private float hide = -1f;         // >0 - прячется (уменьшается до исчезновения)
    private float appear = 1f;
    private Vector3 baseScale;
    private Transform player;
    private string animState = "Run";

    // Один раз за игру одна крыса не убегает, а останавливается и молча смотрит на игрока.
    // Исчезает, как только игрок отведёт взгляд.
    public static bool StareUsed;
    private bool staring;
    private float stareStart, lookAway;

    public void Run(IList<Vector3> points, Transform playerTransform)
    {
        path.Clear();
        path.AddRange(points);
        corner = 1;
        player = playerTransform;
        appear = 0f;
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
        transform.position = path[0];
        if (path.Count > 1) FaceTo(path[1], 1000f);
        nextPauseCheck = Time.time + Random.Range(0.6f, 1.5f);
        nextSqueak = Time.time + Random.Range(0.2f, 1.5f);
    }

    void Awake()
    {
        baseScale = transform.localScale;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (animator != null) animator.Play("Run", 0, Random.value);
        if (audioSource != null && patter != null)
        {
            audioSource.clip = patter;
            audioSource.loop = true;
            audioSource.pitch = Random.Range(0.9f, 1.15f);
            audioSource.Play();
        }
    }

    void Update()
    {
        // Появляется из щели: быстро "вырастает"
        if (appear < 1f)
        {
            appear = Mathf.Min(1f, appear + Time.deltaTime * 5f);
            transform.localScale = baseScale * appear;
        }

        if (hide >= 0f)
        {
            hide += Time.deltaTime * 4f;
            transform.localScale = baseScale * Mathf.Max(0f, 1f - hide);
            if (audioSource != null) audioSource.volume *= 0.9f;
            if (hide >= 1f) Destroy(gameObject);
            return;
        }

        if (staring || TryStartStare())
        {
            Stare();
            return;
        }

        bool scared = player != null && Vector3.Distance(player.position, transform.position) < fleeDistance;
        bool paused = Time.time < pauseUntil && !scared;

        // Иногда замирает на полпути
        if (!paused && !scared && Time.time >= nextPauseCheck)
        {
            nextPauseCheck = Time.time + Random.Range(1f, 2.5f);
            if (Random.value < pauseChance) { pauseUntil = Time.time + Random.Range(0.4f, 1.6f); paused = true; }
        }

        float v = 0f;
        if (!paused && corner < path.Count)
        {
            float s = scared ? fleeSpeed : speed;
            Vector3 target = path[corner];
            Vector3 pos = Vector3.MoveTowards(transform.position, target, s * Time.deltaTime);
            v = s;
            FaceTo(target, turnSpeed);
            transform.position = pos;
            if ((pos - target).sqrMagnitude < 0.0025f) corner++;
        }
        else if (corner >= path.Count)
        {
            hide = 0f;   // добежала - юркнула в щель
        }

        // Бежит - анимация бега со скоростью движения, стоит - принюхивается (Idle)
        if (animator != null)
        {
            string state = v > 0f ? "Run" : "Idle";
            if (state != animState)
            {
                animState = state;
                animator.CrossFadeInFixedTime(state, 0.12f);
            }
            animator.speed = v > 0f ? v * animPerMeter : 1f;
        }

        if (audioSource != null) audioSource.volume = Mathf.MoveTowards(audioSource.volume, v > 0f ? 0.85f : 0f, Time.deltaTime * 6f);
        if (Time.time >= nextSqueak && squeaks != null && squeaks.Length > 0 && audioSource != null)
        {
            audioSource.PlayOneShot(squeaks[Random.Range(0, squeaks.Length)], Random.Range(0.6f, 1f) * (scared ? 1.3f : 1f));
            nextSqueak = Time.time + Random.Range(1.5f, 5f) * (scared ? 0.5f : 1f);
        }
    }

    // Игрок смотрит прямо на крысу, и между ними ничего нет
    bool PlayerLooksAtMe(float minDot)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 me = transform.position + Vector3.up * 0.25f;
        Vector3 dir = me - cam.transform.position;
        if (Vector3.Dot(cam.transform.forward, dir.normalized) < minDot) return false;
        // крыса у самого пола - последние 30 см луча не проверяем, иначе он цепляет пол
        return !Physics.Raycast(cam.transform.position, dir.normalized, dir.magnitude - 0.3f, ~0, QueryTriggerInteraction.Ignore);
    }

    bool TryStartStare()
    {
        if (StareUsed || player == null || appear < 1f) return false;
        if (QuestManager.Instance == null || QuestManager.Instance.PlayTime < 120f) return false;
        float d = Vector3.Distance(player.position, transform.position);
        if (d < 2.5f || d > 8f || !PlayerLooksAtMe(0.8f)) return false;
        StareUsed = true;
        staring = true;
        stareStart = Time.time;
        return true;
    }

    void Stare()
    {
        Vector3 cam = Camera.main != null ? Camera.main.transform.position : player.position;
        FaceTo(cam, 240f);
        if (animator != null)
        {
            if (animState != "Idle") { animState = "Idle"; animator.CrossFadeInFixedTime("Idle", 0.2f); }
            animator.speed = 0.35f;   // почти не шевелится
        }
        if (audioSource != null) audioSource.volume = Mathf.MoveTowards(audioSource.volume, 0f, Time.deltaTime * 6f);

        // Отвернулся (после пары секунд взгляда) или ушёл - крысы уже нет, мгновенно, без бега
        bool looking = PlayerLooksAtMe(0.55f);
        lookAway = looking ? 0f : lookAway + Time.deltaTime;
        float d = Vector3.Distance(player.position, transform.position);
        if ((Time.time - stareStart > 2f && lookAway > 0.35f) || d > 14f || Time.time - stareStart > 30f)
            Destroy(gameObject);
    }

    void FaceTo(Vector3 target, float degPerSec)
    {
        Vector3 dir = target - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
    }
}
