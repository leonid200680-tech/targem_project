using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class IntroSequence : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject blackScreen;

    [Header("Моргание")]
    public int blinkCount = 3;          // Сколько раз моргнуть
    public float fadeOutTime = 0.5f;    // Время, за которое глаза ЗАКРЫВАЮТСЯ (темнеет)
    public float waitClosedTime = 0.2f; // Время, которое глаза остаются ЗАКРЫТЫМИ
    public float fadeInTime = 0.8f;     // Время, за которое глаза ОТКРЫВАЮТСЯ (светлеет)

    private PlayerController playerController;
    private NavMeshAgent ghostAgent;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        GhostChase ghost = FindObjectOfType<GhostChase>();
        if (ghost != null) ghostAgent = ghost.GetComponent<NavMeshAgent>();

        // Отключаем управление на время вступления
        if (playerController != null) playerController.enabled = false;

        StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        if (blackScreen != null)
        {
            blackScreen.SetActive(true);
            Image img = blackScreen.GetComponent<Image>();

            // 1. Начинаем с полностью черного экрана (глаза закрыты)
            img.color = new Color(0, 0, 0, 1f);

            // 2. Ждем чуть-чуть в самом начале
            yield return new WaitForSeconds(1f);

            // 3. Цикл моргания
            for (int i = 0; i < blinkCount; i++)
            {
                // Открываем глаза (экран становится прозрачным)
                float t = 0;
                while (t < fadeInTime)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, t / fadeInTime);
                    img.color = new Color(0, 0, 0, alpha);
                    yield return null;
                }

                // Если это не последнее моргание, то закрываем глаза обратно
                if (i < blinkCount - 1)
                {
                    yield return new WaitForSeconds(waitClosedTime); // Держим глаза открытыми

                    // Закрываем глаза (экран темнеет)
                    t = 0;
                    while (t < fadeOutTime)
                    {
                        t += Time.deltaTime;
                        float alpha = Mathf.Lerp(0f, 1f, t / fadeOutTime);
                        img.color = new Color(0, 0, 0, alpha);
                        yield return null;
                    }
                    yield return new WaitForSeconds(waitClosedTime); // Держим глаза закрытыми
                }
            }

            // 4. Финальное выключение экрана
            img.color = new Color(0, 0, 0, 0f);
            blackScreen.SetActive(false);
        }

        // 5. Включаем управление игроком
        if (playerController != null) playerController.enabled = true;

        // 6. Включаем бабку, чтобы она погналась за нами
        if (ghostAgent != null) ghostAgent.enabled = true;
    }
}