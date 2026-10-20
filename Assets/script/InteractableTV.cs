using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class InteractableTV : MonoBehaviour
{
    public string prompt = "Нажмите E, чтобы посмотреть камеры";

    [Header("Видео")]
    public GameObject tvVideoPanel;
    public VideoPlayer tvVideoPlayer;

    private bool isPlaying = false;

    public void Interact()
    {
        if (isPlaying) return;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnTVWatched();

        StartCoroutine(PlayVideoSequence());
    }

    IEnumerator PlayVideoSequence()
    {
        isPlaying = true;

        if (tvVideoPanel != null && tvVideoPlayer != null)
        {
            tvVideoPanel.SetActive(true);
            tvVideoPlayer.Prepare();
            while (!tvVideoPlayer.isPrepared) yield return null;

            tvVideoPlayer.Play();
            yield return new WaitForSeconds((float)tvVideoPlayer.clip.length);

            tvVideoPanel.SetActive(false);
        }

        isPlaying = false;
    }
}