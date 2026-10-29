using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Настройки")]
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 4f;
    public List<Transform> waypoints;

    private NavMeshAgent agent;
    private Transform player;
    private bool canKill = false;

    void Start()
    {
        Initialize();
    }

    // Отдельный метод инициализации, чтобы вызывать его принудительно
    void Initialize()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null || agent == null || !agent.enabled) return;

        // ... (тут твоя логика зрения и патрулирования, если она была)
    }

    public void ScreamerDash()
    {
        // Принудительно инициализируем переменные перед запуском
        Initialize();

        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        StartCoroutine(ScreamerDashRoutine());
    }

    private IEnumerator ScreamerDashRoutine()
    {
        // ЕЩЕ РАЗ ПРОВЕРЯЕМ, ЧТО АГЕНТ ЕСТЬ
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError("ОШИБКА: На бабке нет компонента NavMeshAgent!");
            yield break; // Прерываем, чтобы не было ошибки
        }

        canKill = false;
        agent.speed = 15f;

        if (player != null)
        {
            Vector3 spawnPos = player.position + player.forward * 8f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }

            float timer = 1f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (agent != null && agent.enabled && player != null)
                    agent.SetDestination(player.position);
                yield return null;
            }
        }

        gameObject.SetActive(false);
    }

    public bool CanKillPlayer() { return canKill; }
}