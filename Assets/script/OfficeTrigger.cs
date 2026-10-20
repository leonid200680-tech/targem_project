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
            StartCoroutine(PlaySequence(other.GetComponent<PlayerController>()));
        }
    }

    IEnumerator PlaySequence(PlayerController playerController)
    {
        // 1. Закрываем и запираем дверь
        if (doorToLock != null) doorToLock.LockDoor();

        // 2. Обновляем квест
        if (QuestManager.Instance != null) QuestManager.Instance.OnOfficeReached();

        // 3. Отключаем управление (опционально, чтобы игрок просто смотрел)
        if (playerController != null) playerController.enabled = false;

        // 4. Показываем видео
        if (videoPanel != null && videoPlayer != null)
        {
            videoPanel.SetActive(true);
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;

            videoPlayer.Play();
            yield return new WaitForSeconds((float)videoPlayer.clip.length);

            videoPanel.SetActive(false);
        }

        // 5. Возвращаем управление
        if (playerController != null) playerController.enabled = true;
    }
}