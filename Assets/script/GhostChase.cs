using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Настройки зрения")]
    public float viewDistance = 10f;
    public float viewAngle = 60f;
    public LayerMask obstacleMask;

    [Header("Патрулирование")]
    public List<Transform> waypoints;
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 4f;

    private NavMeshAgent agent;
    private Transform player;
    private bool canKill = false;
    private float forceChaseTimer = 0f;
    private Vector3 lastKnownPosition; // Где она видела игрока в последний раз

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        agent.speed = walkSpeed;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Если игрок спрятался в шкафу, она его не видит
        if (HideableCloset.IsPlayerHidden)
        {
            agent.speed = walkSpeed;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                GoToNextWaypoint(); // Уходит гулять
            return;
        }

        // Принудительная погоня
        if (forceChaseTimer > 0)
        {
            forceChaseTimer -= Time.deltaTime;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
            return;
        }

        // Обычная логика зрения
        bool canSeePlayer = CanSeePlayer();
        if (canSeePlayer)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
            lastKnownPosition = player.position; // Запоминаем, где он
        }
        else
        {
            agent.speed = walkSpeed;

            // Если она не видит игрока, она идет к месту, где видела его в последний раз
            if (lastKnownPosition != Vector3.zero)
            {
                agent.SetDestination(lastKnownPosition);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    lastKnownPosition = Vector3.zero; // Дошла, теперь можно гулять
                }
            }
            else if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                GoToNextWaypoint();
            }
        }
    }

    public void ForceChase()
    {
        forceChaseTimer = 10f;
        canKill = true;
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

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        int randomIndex = Random.Range(0, waypoints.Count);
        agent.SetDestination(waypoints[randomIndex].position);
    }

    public bool CanKillPlayer() { return canKill; }
}