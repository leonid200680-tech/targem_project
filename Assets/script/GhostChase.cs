using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Настройки патрулирования")]
    public List<Transform> waypoints;
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 4f;

    [Header("Зрение")]
    public float viewDistance = 10f;
    public float viewAngle = 60f;
    public LayerMask obstacleMask;

    [Header("Рост по камням: со 2-го камня к 5-му значения доходят до этих")]
    public float maxWalkSpeed = 2.4f;
    public float maxChaseSpeed = 5.2f;    // игрок бежит 7: от неё можно оторваться
    public float maxViewDistance = 20f;
    public float maxViewAngle = 150f;
    [Tooltip("Если игрока уже ловили дважды, погоня не быстрее этого: игра остаётся проходимой")]
    public float mercyChaseSpeed = 4.6f;

    private float baseWalk, baseChase, baseView, baseAngle;
    private int appliedStones = -1, appliedLost = -1;
    private bool crawling, crawlDelay;
    private GrannySlow slow;

    private NavMeshAgent agent;
    private Animator anim;
    private Transform player;

    private bool canKill = false;
    private float forceChaseTimer = 0f;
    private Vector3 lastKnownPosition = Vector3.zero;

    // Сторож от застревания (лестницы, узкие проходы)
    private Vector3 stuckCheckPos;
    private float stuckTimer;
    private int stuckCount;

    public bool Scripted { get; private set; }
    public bool Peeking { get; private set; }   // стоит у шкафа лицом к дверцам

    // Игрок спрятался в шкафу: бабка подходит вплотную к дверцам, заглядывает в решётку
    // и через несколько секунд уходит, так и не тронув его. Если игрок вылезет раньше - обычная погоня.
    public bool TryPeekCloset(Vector3 stand, Vector3 faceDir, float hold)
    {
        if (crawling || Scripted || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;   // на четвереньках к решётке не встать
        if (!NavMesh.SamplePosition(stand, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        float len = 0f;
        for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        if (len > 30f) return false;
        StartCoroutine(PeekRoutine(hit.position, faceDir, hold));
        return true;
    }

    System.Collections.IEnumerator PeekRoutine(Vector3 stand, Vector3 faceDir, float hold)
    {
        Scripted = true;
        forceChaseTimer = 0f;
        lastKnownPosition = Vector3.zero;
        agent.speed = walkSpeed * 1.3f;
        agent.SetDestination(stand);

        float t = 0f;
        while (t < 20f && HideableCloset.IsPlayerHidden && (agent.pathPending || agent.remainingDistance > 0.25f))
        {
            t += Time.deltaTime;
            if (anim != null) anim.SetFloat("Speed", agent.velocity.magnitude);
            yield return null;
        }
        if (!HideableCloset.IsPlayerHidden) { Scripted = false; yield break; }

        // Встала у дверец, поворачивается к решётке и наклоняется к щелям
        agent.isStopped = true;
        agent.updateRotation = false;
        if (anim != null) anim.SetFloat("Speed", 0f);
        Quaternion from = transform.rotation, to = Quaternion.LookRotation(new Vector3(faceDir.x, 0f, faceDir.z));
        // Лицо - к щелям решётки (на уровень глаз игрока): бабка ниже охранника, приподнимаем её
        var head0 = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        var cam = Camera.main;

        float lift = head0 != null && cam != null ? Mathf.Clamp(cam.transform.position.y - 0.08f - head0.position.y, 0f, 0.5f) : 0f;
        // и придвигаем вплотную к дверцам (навмеш не пускает ближе полуметра) - лицо у самой щели
        agent.updatePosition = false;
        Vector3 standPos = transform.position;
        Vector3 leanPos = standPos + to * Vector3.forward * 0.22f + Vector3.up * lift;
        for (float r = 0f; r < 0.9f; r += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, r / 0.9f);
            transform.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(r / 0.6f));
            transform.position = Vector3.Lerp(standPos, leanPos, k);
            yield return null;
        }
        transform.position = leanPos;

        // Тусклый свет на лице, чтобы сквозь щели было видно глаза
        var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        var lightGo = new GameObject("Peek Face Light");
        lightGo.transform.SetParent(head != null ? head : transform, false);
        lightGo.transform.localPosition = head != null ? Vector3.zero : Vector3.up * 1.5f;
        lightGo.transform.position += to * Vector3.forward * 0.12f + Vector3.down * 0.12f;
        var l = lightGo.AddComponent<Light>();
        var hd = lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        l.type = LightType.Point;
        l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        l.intensity = 150f;
        l.range = 1.8f;
        l.color = new Color(1f, 0.8f, 0.65f);
        l.shadows = LightShadows.None;
        hd.volumetricDimmer = 0f;

        Peeking = true;
        for (float h = 0f; h < hold && HideableCloset.IsPlayerHidden; h += Time.deltaTime) yield return null;
        Peeking = false;
        yield return new WaitForSeconds(0.6f);   // дверца успевает закрыться

        Destroy(lightGo);
        // отстраняется обратно и снова идёт по навмешу
        for (float r = 0f; r < 0.5f; r += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(leanPos, standPos, r / 0.5f);
            yield return null;
        }
        transform.position = standPos;
        agent.nextPosition = standPos;
        agent.updatePosition = true;
        agent.isStopped = false;
        agent.updateRotation = true;
        Scripted = false;
        if (HideableCloset.IsPlayerHidden) GoFarFrom(stand);   // уходит подальше от шкафа
    }

    void GoFarFrom(Vector3 point)
    {
        if (waypoints == null || waypoints.Count == 0) return;
        Transform far = waypoints[0];
        foreach (var w in waypoints)
            if (w != null && Vector3.Distance(w.position, point) > Vector3.Distance(far.position, point)) far = w;
        agent.speed = walkSpeed;
        agent.SetDestination(far.position);
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        slow = GetComponent<GrannySlow>();
        baseWalk = walkSpeed; baseChase = chaseSpeed; baseView = viewDistance; baseAngle = viewAngle;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (agent != null) agent.speed = walkSpeed;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null || agent == null || !agent.enabled) return;

        ApplyStoneStage();
        KeepCrawling();

        // --- ПЕРЕДАЧА СКОРОСТИ В ANIMATOR ---
        if (anim != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            anim.SetFloat("Speed", currentSpeed);
            // (Это единственная строка, которая нужна для Blend Tree)
        }

        // Заскриптованный момент (подходит к шкафу и заглядывает в решётку) - обычная логика ждёт
        if (Scripted) return;

        // Если игрок в шкафу
        if (HideableCloset.IsPlayerHidden)
        {
            agent.speed = walkSpeed;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                GoToNextWaypoint();
            WatchStuck();
            return;
        }

        // Если идет принудительная погоня
        if (forceChaseTimer > 0)
        {
            forceChaseTimer -= Time.deltaTime;
            agent.speed = chaseSpeed;
            agent.SetDestination(OnMesh(player.position));
            WatchStuck();
            return;
        }

        // Обычная логика зрения
        bool canSeePlayer = CanSeePlayer();
        if (canSeePlayer)
        {
            agent.speed = chaseSpeed;
            lastKnownPosition = OnMesh(player.position);
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            agent.speed = walkSpeed;

            if (lastKnownPosition != Vector3.zero)
            {
                agent.SetDestination(lastKnownPosition);
                if (!agent.pathPending && (agent.remainingDistance < 0.5f || agent.pathStatus != NavMeshPathStatus.PathComplete))
                    lastKnownPosition = Vector3.zero;
            }
            else if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                GoToNextWaypoint();
            }
        }
        WatchStuck();
    }

    // Точка на навмеше под игроком. На лестнице ближайшая точка по прямой может оказаться
    // на другом этаже (над/под ступенями), поэтому ищем сначала совсем рядом с ногами.
    Vector3 OnMesh(Vector3 p)
    {
        foreach (float r in new[] { 0.6f, 1.2f, 2.5f })
            if (NavMesh.SamplePosition(p + Vector3.up * 0.1f, out NavMeshHit hit, r, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < 1.2f)
                return hit.position;
        return p;
    }

    // Если бабка хочет идти, но почти не сдвинулась за 1.5 с - пересчитываем путь,
    // а если застряла несколько раз подряд - переставляем её чуть вперёд по пути.
    void WatchStuck()
    {
        // pathPending не проверяем: при погоне путь задаётся каждый кадр, и таймер застревания всё время сбрасывался
        if (agent.isStopped || agent.speed < 0.3f || agent.remainingDistance < 1f)
        {
            stuckTimer = 0f; stuckCheckPos = transform.position; stuckCount = 0;
            return;
        }
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.5f) return;
        float moved = Vector3.Distance(transform.position, stuckCheckPos);
        stuckTimer = 0f;
        stuckCheckPos = transform.position;
        if (moved > 0.35f) { stuckCount = 0; return; }

        stuckCount++;
        Vector3 dest = agent.destination;
        if (stuckCount >= 2 && agent.path.corners.Length > 1)
        {
            // Шаг вперёд по пути, к следующему углу
            Vector3 next = agent.path.corners[1];
            Vector3 step = Vector3.MoveTowards(transform.position, next, 0.8f);
            if (NavMesh.SamplePosition(step, out NavMeshHit hit, 1f, NavMesh.AllAreas)) agent.Warp(hit.position);
            stuckCount = 0;
        }
        agent.ResetPath();
        if (lastKnownPosition == Vector3.zero && forceChaseTimer <= 0f && (HideableCloset.IsPlayerHidden || !CanSeePlayer()))
            GoToNextWaypoint();
        else
            agent.SetDestination(dest);
    }

    public void ForceChase()
    {
        forceChaseTimer = 10f;
        canKill = true;
    }

    // После того как игрок пришёл в себя: бабка забывает о нём и уходит
    // к самой дальней от него точке патруля
    public void SendFarFrom(Vector3 point)
    {
        forceChaseTimer = 0f;
        lastKnownPosition = Vector3.zero;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (waypoints == null || waypoints.Count == 0 || agent == null) return;
        Transform far = waypoints[0];
        foreach (var w in waypoints)
            if (w != null && Vector3.Distance(w.position, point) > Vector3.Distance(far.position, point)) far = w;
        if (agent.enabled) agent.Warp(far.position);
        else transform.position = far.position;
    }
    public void PlayScreamAnimation()
    {
        if (anim != null) anim.SetTrigger("Scream");
    }

    public bool CanKillPlayer()
    {
        return canKill;
    }

    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        if (distance > viewDistance) return false;

        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle / 2f) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer.normalized, out RaycastHit hit, distance))
        {
            return hit.collider.CompareTag("Player");
        }
        return true;
    }

    // С каждым камнем быстрее и зорче, с последним встаёт на четвереньки
    void ApplyStoneStage()
    {
        int stones = PickupItem.stonesCollected, lost = Sanity.Lost;
        if (stones == appliedStones && lost == appliedLost) return;
        appliedStones = stones;
        appliedLost = lost;

        float k = Mathf.Clamp01((stones - 2) / 3f);
        walkSpeed = Mathf.Lerp(baseWalk, maxWalkSpeed, k);
        chaseSpeed = Mathf.Lerp(baseChase, maxChaseSpeed, k);
        if (lost >= 2) chaseSpeed = Mathf.Min(chaseSpeed, mercyChaseSpeed);
        viewDistance = Mathf.Lerp(baseView, maxViewDistance, k);
        viewAngle = Mathf.Lerp(baseAngle, maxViewAngle, k);

        if (k >= 1f && !crawling)
        {
            crawling = true;
            OutdoorSound.HushFor(4.5f);
            StartCoroutine(BeginSurfaceCrawl());
        }
    }

    System.Collections.IEnumerator BeginSurfaceCrawl()
    {
        crawlDelay = true;
        yield return new WaitForSeconds(3f);   // тишина перед превращением
        crawlDelay = false;
        gameObject.AddComponent<GrannySurfaceCrawl>();
        // Вопль слышен издалека и с её стороны: игрок понимает, что она изменилась
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        Vector3 toGhost = transform.position - player.position;
        if (hm != null && hm.screamSound != null)
            AudioSource.PlayClipAtPoint(hm.screamSound, player.position + Vector3.ClampMagnitude(toGhost, 8f), 0.9f);
    }

    void KeepCrawling()
    {
        if (!crawling || crawlDelay || anim == null) return;
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName("crawl") && !anim.IsInTransition(0)) anim.CrossFade("crawl", 0.25f);
        if (slow != null) slow.animRate = Mathf.Clamp(agent.velocity.magnitude / 1.5f, 0f, 2.4f);   // руки и ноги в такт скорости
    }

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        int randomIndex = Random.Range(0, waypoints.Count);
        agent.SetDestination(waypoints[randomIndex].position);
    }
}