using UnityEngine;

public class InteractableDoor : MonoBehaviour
{
    // Настройки двери
    public float openAngle = 90f;
    public float speed = 3f;
    public string prompt = "Нажмите E, чтобы открыть";

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        // Запоминаем начальный поворот двери
        closedRotation = transform.localRotation;
        // Вычисляем поворот для открытого состояния
        openRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        // Плавно открываем или закрываем дверь
        if (isOpen)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, openRotation, Time.deltaTime * speed);
        else
            transform.localRotation = Quaternion.Slerp(transform.localRotation, closedRotation, Time.deltaTime * speed);
    }

    // Метод, который вызовет игрок
    public void Interact()
    {
        isOpen = !isOpen; // Переключаем состояние
    }
}