using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class HallucinationManager : MonoBehaviour
{
    [Header("UI и Звук")]
    public Image jumpscareImage;
    public AudioSource audioSource;
    public AudioClip noiseSound;
    public AudioClip screamSound;

    [Header("Сердцебиение")]
    public AudioClip heartbeatSound;
    public float maxHearDistance = 15f; // С какого расстояния начинается стук

    [Header("Бабка")]
    public GameObject realGhost; // ПЕРЕТАЩИ СЮДА НАСТОЯЩУЮ БАБКУ

    void Start()
    {
        PickupItem.stonesCollected = 0;
        if (jumpscareImage != null) jumpscareImage.gameObject.SetActive(false);
    }

    void Update()
    {
        HandleHeartbeat();
    }

    void HandleHeartbeat()
    {
        if (realGhost == null || !realGhost.activeSelf || heartbeatSound == null || audioSource == null)
            return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        float dist = Vector3.Distance(realGhost.transform.position, playerObj.transform.position);

        // Если бабка близко
        if (dist < maxHearDistance)
        {
            // Включаем звук, если он еще не играет
            if (!audioSource.isPlaying || audioSource.clip != heartbeatSound)
            {
                audioSource.clip = heartbeatSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Чем ближе, тем громче (от 0 до 1) и быстрее (от 1 до 2)
            float proximity = 1f - (dist / maxHearDistance);
            audioSource.volume = Mathf.Lerp(0.1f, 1f, proximity);
            audioSource.pitch = Mathf.Lerp(1f, 1.8f, proximity);
        }
        else
        {
            // Если бабка далеко, останавливаем стук
            if (audioSource.clip == heartbeatSound && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    public void PlayCreepyNoise()
    {
        if (audioSource != null && noiseSound != null) audioSource.PlayOneShot(noiseSound);
    }

    public void TriggerSecondStoneEvent()
    {
        StartCoroutine(SecondStoneRoutine());
    }

    IEnumerator SecondStoneRoutine()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) yield break;

        // 1. СКРИМЕР
        if (audioSource != null && screamSound != null)
            audioSource.PlayOneShot(screamSound, 1f);

        if (jumpscareImage != null)
            jumpscareImage.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.5f);

        if (jumpscareImage != null)
            jumpscareImage.gameObject.SetActive(false);

        // 2. СПАВН БАБКИ
        if (realGhost != null)
        {
            realGhost.SetActive(true);

            GhostChase chase = realGhost.GetComponent<GhostChase>();
            if (chase != null) chase.enabled = true;

            NavMeshAgent agent = realGhost.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = true;

            Vector3 spawnPos = playerObj.transform.position + playerObj.transform.forward * 15f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
            {
                realGhost.transform.position = hit.position;
            }
        }
    }
}