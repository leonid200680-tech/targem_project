using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GhostKiller : MonoBehaviour
{
    [Header("Настройки скримера")]
    public float killDistance = 1.5f;

    [Header("Ссылки")]
    public GameObject jumpscareVideo;
    public GameObject mainMenuPanel;

    private VideoPlayer videoPlayer;
    private bool isDead = false;
    private Transform player;

    void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        if (jumpscareVideo != null)
            videoPlayer = jumpscareVideo.GetComponent<VideoPlayer>();
    }

    void Update()
    {
        if (player == null || isDead) return;

        // Если бабка близко
        if (Vector3.Distance(transform.position, player.position) <= killDistance)
        {
            StartCoroutine(DieSequence());
        }
    }

    IEnumerator DieSequence()
    {
        isDead = true;

        // 1. Отключаем управление
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;

        if (jumpscareVideo != null && videoPlayer != null)
        {
            // 2. Включаем объект с видео
            jumpscareVideo.SetActive(true);

            // 3. Готовим видео (очень важно для HDRP)
            videoPlayer.Prepare();

            // 4. Ждем, пока видео подготовится (обычно доля секунды)
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            // 5. Играем видео
            videoPlayer.Play();

            // 6. Ждем, пока видео проиграется. 
            // (clip.length — это длина видео в секундах).
            yield return new WaitForSeconds((float)videoPlayer.clip.length);

            // 7. Выключаем видео
            jumpscareVideo.SetActive(false);
        }

        // 8. Включаем меню
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // 9. Разблокируем курсор (чтобы можно было нажимать кнопки в меню)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}