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
    private Light glow;   // экран светится, чтобы телефон был заметен на столе

    void Start()
    {
        originalPosition = transform.localPosition;
        if (ringAudioSource != null) ringAudioSource.Stop();
    }

    void Update()
    {
        if (glow != null)
        {
            glow.enabled = isRinging;
            glow.intensity = Mathf.Lerp(40f, 160f, Mathf.PingPong(Time.time * 2f, 1f));
        }
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

    void MakeGlow()
    {
        var go = new GameObject("Phone Glow");
        go.transform.position = transform.position + Vector3.up * 0.2f;
        glow = go.AddComponent<Light>();
        go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        glow.type = LightType.Point;
        glow.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        glow.range = 3f;
        glow.color = new Color(0.6f, 0.8f, 1f);
        glow.shadows = LightShadows.None;
    }

    public void StartRinging()
    {
        if (isRinging) return;
        if (glow == null) MakeGlow();

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