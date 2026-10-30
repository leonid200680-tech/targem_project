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

    private NavMeshAgent agent;
    private Animator anim;
    private Transform player;

    private bool canKill = false;
    private float forceChaseTimer = 0f;
    private Vector3 lastKnownPosition = Vector3.zero;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (agent != null) agent.speed = walkSpeed;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null || agent == null || !agent.enabled) return;

        // --- ПЕРЕДАЧА СКОРОСТИ В ANIMATOR ---
        if (anim != null)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

        // Если игрок в шкафу
        if (HideableCloset.IsPlayerHidden)
        {
            agent.speed = walkSpeed;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                GoToNextWaypoint();
            return;
        }

        // Если идет принудительная погоня
        if (forceChaseTimer > 0)
        {
            forceChaseTimer -= Time.deltaTime;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
            return;
        }

        // Обычная логика
        bool canSeePlayer = CanSeePlayer();
        if (canSeePlayer)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
            lastKnownPosition = player.position;
        }
        else
        {
            agent.speed = walkSpeed;

            if (lastKnownPosition != Vector3.zero)
            {
                agent.SetDestination(lastKnownPosition);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                    lastKnownPosition = Vector3.zero;
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

    public void PlayScreamAnimation()
    {
        if (anim != null) anim.SetTrigger("Scream");
    }

    // ЭТОТ МЕТОД ИСКАЛ GHOSTKILLER
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

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        int randomIndex = Random.Range(0, waypoints.Count);
        agent.SetDestination(waypoints[randomIndex].position);
    }
}