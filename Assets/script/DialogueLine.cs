using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speakerName;      // Имя (Мама, Охранник)
    [TextArea(3, 10)]
    public string text;             // Сам текст субтитров
    public AudioClip voiceClip;     // Аудиофайл озвучки
    public float delayBeforeNext;   // Пауза после окончания озвучки (если аудио нет)
}