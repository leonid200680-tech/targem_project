using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private EnemyTrigger trigger;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        trigger = GetComponent<EnemyTrigger>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log("[Ghost] Игрок найден: " + playerObj.name);
        }
        else
        {
            Debug.LogError("[Ghost] Не найден объект с тегом 'Player'!");
        }

        Debug.Log("[Ghost] Агент на NavMesh: " + agent.isOnNavMesh);
    }

    void Update()
    {
        if (player == null || trigger == null) return;

        Debug.Log("[Ghost] Триггер: " + trigger.playerDetected);

        if (trigger.playerDetected)
        {
            bool ok = agent.SetDestination(player.position);
            Debug.Log("[Ghost] SetDestination = " + ok + " | pathStatus = " + agent.pathStatus + " | isOnNavMesh = " + agent.isOnNavMesh);
        }
    }
}