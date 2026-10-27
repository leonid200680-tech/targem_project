using UnityEngine;

public class PhoneController : MonoBehaviour
{
    [Header("Настройки звонка")]
    public AudioSource ringAudioSource;      // Источник звука гудков
    public DialogueTrigger dialogueTrigger;  // Ссылка на скрипт диалога

    [Header("Вибрация")]
    public float shakeIntensity = 0.005f;    // Сила тряски (не делай слишком большой)
    public float shakeSpeed = 50f;           // Скорость тряски

    private Vector3 originalPosition;
    private bool isRinging = false;

    void Start()
    {
        originalPosition = transform.localPosition;
        if (ringAudioSource != null) ringAudioSource.Stop();
    }

    void Update()
    {
        if (isRinging)
        {
            // Формула тряски (вибрация)
            Vector3 shake = new Vector3(
                Mathf.Sin(Time.time * shakeSpeed) * shakeIntensity,
                Mathf.Cos(Time.time * shakeSpeed * 1.2f) * shakeIntensity,
                Mathf.Sin(Time.time * shakeSpeed * 0.8f) * shakeIntensity
            );
            transform.localPosition = originalPosition + shake;
        }
        else
        {
            // Возвращаем на место, если не звонит
            transform.localPosition = originalPosition;
        }
    }

    public void StartRinging()
    {
        if (isRinging) return;

        isRinging = true;
        if (ringAudioSource != null) ringAudioSource.Play();
    }

    public void AnswerPhone()
    {
        isRinging = false;
        if (ringAudioSource != null) ringAudioSource.Stop();

        if (dialogueTrigger != null) dialogueTrigger.TriggerDialogue();
    }

    public bool IsRinging()
    {
        return isRinging;
    }
}