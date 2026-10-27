using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class OfficeTrigger : MonoBehaviour
{
    [Header("Видео на экране")]
    public GameObject videoPanel;
    public VideoPlayer videoPlayer;

    [Header("Дверь")]
    public InteractableDoor doorToLock;

    [Header("Телепортация после видео")]
    public Transform spawnPoint;

    private bool hasTriggered = false;

    void Start()
    {
        if (videoPanel != null) videoPanel.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            StartCoroutine(PlaySequence(other.GetComponent<PlayerController>(), other.gameObject));
        }
    }

    IEnumerator PlaySequence(PlayerController pc, GameObject playerObj)
    {
        if (doorToLock != null) doorToLock.LockDoor();
        if (QuestManager.Instance != null) QuestManager.Instance.OnOfficeReached();

        // Отключаем управление
        if (pc != null) pc.enabled = false;

        // Показываем видео
        if (videoPanel != null && videoPlayer != null)
        {
            videoPanel.SetActive(true);
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;
            videoPlayer.Play();
            yield return new WaitForSeconds((float)videoPlayer.clip.length);
            videoPanel.SetActive(false);
        }

        // ТЕЛЕПОРТАЦИЯ
        if (spawnPoint != null)
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();

            // 1. Отключаем контроллер
            if (cc != null) cc.enabled = false;

            // 2. Перемещаем игрока
            playerObj.transform.position = spawnPoint.position;
            playerObj.transform.rotation = spawnPoint.rotation;

            // 3. Включаем контроллер
            if (cc != null) cc.enabled = true;

            Debug.Log("Игрок телепортирован на точку: " + spawnPoint.name);
        }

        // Возвращаем управление
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}