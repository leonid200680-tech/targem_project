using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Настройки зрения")]
    public float viewDistance = 8f;
    public float viewAngle = 60f;
    public LayerMask obstacleMask;

    [Header("Патрулирование (прогулка)")]
    public List<Transform> waypoints;
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 3.5f;
    public float waitAtPointTime = 3f;

    [Header("Поиск игрока")]
    public float searchWaitTime = 4f; // Сколько секунд она стоит на месте, где видела игрока

    private NavMeshAgent agent;
    private Transform player;
    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;

    // Состояния бабки
    private enum State { Patrolling, Chasing, Searching }
    private State currentState = State.Patrolling;

    private Vector3 lastKnownPosition;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        agent.speed = walkSpeed;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null) return;

        // Проверяем, видит ли она игрока прямо сейчас
        bool canSeePlayer = CanSeePlayer();

        if (canSeePlayer)
        {
            // Если видит — переходит в режим погони
            lastKnownPosition = player.position; // Запоминаем, где он

            if (currentState != State.Chasing)
            {
                currentState = State.Chasing;
                agent.speed = chaseSpeed;
                if (animator != null) animator.SetBool("isRunning", true);
            }

            agent.SetDestination(player.position);
        }
        else
        {
            // Если не видит
            if (currentState == State.Chasing)
            {
                // Только что потеряла из виду! Переходим в режим поиска
                currentState = State.Searching;
                agent.speed = walkSpeed; // Идет быстрым шагом
                if (animator != null) animator.SetBool("isRunning", false);

                // Идет к последней точке, где видела
                agent.SetDestination(lastKnownPosition);
                waitTimer = 0f;
            }
            else if (currentState == State.Searching)
            {
                // Дошла до места поиска?
                if (!agent.pathPending && agent.velocity.sqrMagnitude < 0.1f)
                {
                    waitTimer += Time.deltaTime;
                    // Если постояла достаточно — сдается и возвращается к патрулю
                    if (waitTimer >= searchWaitTime)
                    {
                        currentState = State.Patrolling;
                        GoToNextWaypoint();
                    }
                }
            }
            else if (currentState == State.Patrolling)
            {
                // Обычная прогулка
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

        if (distance > viewDistance) return false;

        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle / 2f) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer.normalized, out RaycastHit hit, distance))
        {
            if (hit.collider.CompareTag("Player")) return true;
            else return false;
        }

        return true;
    }

    void GoToNextWaypoint()
    {
        waitTimer = 0f;
        if (waypoints == null || waypoints.Count == 0) return;

        currentWaypointIndex = Random.Range(0, waypoints.Count);
        Transform targetPoint = waypoints[currentWaypointIndex];

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPoint.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}