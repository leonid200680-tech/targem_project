using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Настройки зрения")]
    public float viewDistance = 8f;       // Дистанция, с которой она видит
    public float viewAngle = 60f;         // Угол обзора (60 градусов вперёд)
    public LayerMask obstacleMask;        // Слой стен (чтобы не видеть сквозь них)

    [Header("Патрулирование (прогулка)")]
    public List<Transform> waypoints;     // Сюда перетащим точки в Инспекторе
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 3.5f;
    public float waitAtPointTime = 3f;    // Сколько секунд она стоит на точке

    private NavMeshAgent agent;
    private Transform player;
    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;
    private bool isChasing = false;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Начинаем с прогулки
        agent.speed = walkSpeed;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null) return;

        // 1. Проверяем, видим ли мы игрока
        if (CanSeePlayer())
        {
            if (!isChasing)
            {
                isChasing = true;
                agent.speed = chaseSpeed;
                if (animator != null) animator.SetBool("isRunning", true);
                Debug.Log("[Ghost] Увидела игрока и бежит!");
            }

            // Бежим к игроку
            agent.SetDestination(player.position);
        }
        else
        {
            // Если не видим и бежали — перестаем бежать
            if (isChasing)
            {
                isChasing = false;
                agent.speed = walkSpeed;
                if (animator != null) animator.SetBool("isRunning", false);
                Debug.Log("[Ghost] Потеряла игрока из виду. Идет гулять.");
                GoToNextWaypoint();
            }

            // 2. Логика прогулки (если не бежим за игроком)
            if (!isChasing)
            {
                // Если агент не строит путь, не бежит и его скорость почти нулевая (он стоит)
                if (!agent.pathPending && agent.velocity.sqrMagnitude < 0.1f)
                {
                    waitTimer += Time.deltaTime;
                    if (waitTimer >= waitAtPointTime)
                    {
                        GoToNextWaypoint();
                    }
                }
            }
        }
    }

    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        // Проверка дистанции
        if (distance > viewDistance) return false;

        // Проверка угла (в поле зрения ли игрок)
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle / 2f) return false;

        // Проверка стен (Raycast из глаз бабки в игрока)
        // Если луч попадает в стену (obstacleMask), значит она не видит игрока
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer.normalized, out RaycastHit hit, distance))
        {
            if (hit.collider.CompareTag("Player")) return true;
            else return false; // Луч попал в стену/мебель
        }

        return true;
    }

    void GoToNextWaypoint()
    {
        waitTimer = 0f;

        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning("[Ghost] Список точек пуст! Добавь точки в скрипт.");
            return;
        }

        // Выбираем случайную точку, чтобы она не бегала туда-сюда по одной линии
        currentWaypointIndex = Random.Range(0, waypoints.Count);
        Transform targetPoint = waypoints[currentWaypointIndex];

        // Проверяем, находится ли точка на NavMesh (на полу)
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPoint.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning("[Ghost] Точка " + targetPoint.name + " не находится на NavMesh! Проверь её позицию в сцене.");
            // Если точка плохая, вызываем метод еще раз, чтобы взять другую
            GoToNextWaypoint();
        }
    }
}