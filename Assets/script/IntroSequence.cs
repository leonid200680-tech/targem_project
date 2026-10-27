using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class IntroSequence : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject blackScreen;

    [Header("Моргание")]
    public int blinkCount = 2;
    public float fadeOutTime = 0.4f;
    public float waitClosedTime = 0.1f;
    public float fadeInTime = 1.0f;

    private PlayerController playerController;

    void Start()
    {
        playerController = GetComponent<PlayerController>();

        // 1. Сразу отключаем управление
        if (playerController != null) playerController.enabled = false;

        // 2. Запускаем анимацию
        StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        // Если объект не привязан — просто включаем управление и выходим
        if (blackScreen == null)
        {
            if (playerController != null) playerController.enabled = true;
            yield break;
        }

        blackScreen.SetActive(true);
        Image img = blackScreen.GetComponent<Image>();

        // Защита: если на объекте нет компонента Image, мы не можем менять цвет
        if (img == null)
        {
            blackScreen.SetActive(false);
            if (playerController != null) playerController.enabled = true;
            yield break;
        }

        // Ставим полностью черный цвет
        img.color = new Color(0, 0, 0, 1f);

        // Пауза перед первым "открытием глаз"
        yield return new WaitForSeconds(1f);

        // Цикл моргания
        for (int i = 0; i < blinkCount; i++)
        {
            // Открываем глаза (прозрачный экран)
            float t = 0;
            while (t < fadeInTime)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, t / fadeInTime);
                img.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            // Если это не последнее моргание, закрываем глаза обратно
            if (i < blinkCount - 1)
            {
                yield return new WaitForSeconds(waitClosedTime);

                t = 0;
                while (t < fadeOutTime)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(0f, 1f, t / fadeOutTime);
                    img.color = new Color(0, 0, 0, alpha);
                    yield return null;
                }
                yield return new WaitForSeconds(waitClosedTime);
            }
        }

        // Финальное выключение
        img.color = new Color(0, 0, 0, 0f);
        blackScreen.SetActive(false);

        // Возвращаем управление
        if (playerController != null) playerController.enabled = true;
    }
}