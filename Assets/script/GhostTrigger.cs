using System.Collections;
using UnityEngine;

public class GhostTrigger : MonoBehaviour
{
    public GameObject screamerGhost;
    public AudioClip screamSound;
    public float spawnDistance = 8f;
    public bool spawnBehindPlayer = true;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // Лог 1: Кто вообще зашел в триггер
        Debug.Log("В триггер вошел: " + other.name + " | Тег: " + other.tag);

        if (other.CompareTag("Player") && !triggered && PickupItem.stonesCollected >= 1)
        {
            triggered = true;
            Debug.Log("ТРИГГЕР СРАБОТАЛ! Камней собрано: " + PickupItem.stonesCollected);
            StartCoroutine(PlayGhostAttack(other.gameObject));
        }
    }

    IEnumerator PlayGhostAttack(GameObject playerObj)
    {
        if (screamSound != null)
        {
            AudioSource playerAudio = playerObj.GetComponent<AudioSource>();
            if (playerAudio == null) playerAudio = playerObj.AddComponent<AudioSource>();
            playerAudio.PlayOneShot(screamSound, 1f);
        }

        if (screamerGhost != null && playerObj != null)
        {
            Debug.Log("Спавним бабку!");
            Vector3 spawnPos;
            if (spawnBehindPlayer)
                spawnPos = playerObj.transform.position - playerObj.transform.forward * spawnDistance;
            else
                spawnPos = playerObj.transform.position + playerObj.transform.forward * spawnDistance;

            spawnPos.y = playerObj.transform.position.y;
            screamerGhost.transform.position = spawnPos;
            screamerGhost.transform.LookAt(new Vector3(playerObj.transform.position.x, spawnPos.y, playerObj.transform.position.z));
            screamerGhost.SetActive(true); // ВКЛЮЧАЕМ
        }
        else
        {
            Debug.LogError("ОШИБКА: Либо screamerGhost не привязан в инспекторе, либо playerObj пустой!");
        }

        yield return new WaitForSeconds(2.0f);

        if (screamerGhost != null) screamerGhost.SetActive(false);
    }
}