using UnityEngine;
using System.Collections;

public class FlickeringLight : MonoBehaviour
{
    [Header("Компоненты (заполнить в инспекторе)")]
    public Light targetLight;               // Сюда кидаем сам источник света (Light)
    public AudioSource buzzAudio;           // Источник звука (гудение лампы)
    public Renderer bulbRenderer;           // Модель/спрайт самой лампочки (чтобы она тоже мигала)

    [Header("Настройки света")]
    public float minIntensity = 0.2f;       // Минимальная яркость (когда почти потухла)
    public float maxIntensity = 2.5f;       // Максимальная яркость (когда светит)
    public float minWait = 0.05f;           // Мин. пауза между мерцаниями
    public float maxWait = 0.5f;            // Макс. пауза между мерцаниями

    private Material bulbMaterial;
    private float originalIntensity;

    void Start()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();

        originalIntensity = maxIntensity;
        if (targetLight != null) targetLight.intensity = originalIntensity;

        // Если передали модель лампочки, находим её материал
        if (bulbRenderer != null)
        {
            bulbMaterial = bulbRenderer.material;
        }

        if (buzzAudio != null)
        {
            buzzAudio.Play(); // Включаем гудение при старте
        }

        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // 1. Случайная пауза (лампа горит стабильно)
            yield return new WaitForSeconds(Random.Range(1f, 4f));

            // 2. Начинается мерцание (от 3 до 6 вспышек подряд)
            int flickerCount = Random.Range(3, 6);
            for (int i = 0; i < flickerCount; i++)
            {
                // Резко тушим
                SetLightState(false);
                yield return new WaitForSeconds(Random.Range(minWait, maxWait));

                // Резко включаем
                SetLightState(true);
                yield return new WaitForSeconds(Random.Range(minWait, maxWait));
            }

            // 3. Иногда лампа полностью вырубается на пару секунд (черный экран)
            if (Random.value < 0.3f) // 30% шанс, что вырубится дольше
            {
                SetLightState(false);
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                SetLightState(true);
            }
        }
    }

    void SetLightState(bool isOn)
    {
        // Включаем/выключаем яркость самого света
        if (targetLight != null)
        {
            targetLight.intensity = isOn ? maxIntensity : minIntensity;
        }

        // Меняем свечение на самой модели лампочки (Эмиссия)
        if (bulbMaterial != null)
        {
            if (isOn)
            {
                bulbMaterial.EnableKeyword("_EMISSION");
                bulbMaterial.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.5f) * 2f); // Теплый желтый свет
            }
            else
            {
                bulbMaterial.SetColor("_EmissionColor", Color.black); // Лампочка потухла
            }
        }

         if (buzzAudio != null) {
             if (isOn && !buzzAudio.isPlaying) buzzAudio.Play();
             else if (!isOn) buzzAudio.Stop();
        }
    }
}