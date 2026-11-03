using UnityEngine;

public class InteractableDoor : MonoBehaviour
{
    public float openAngle = 90f;
    public float speed = 3f;
    public string prompt = "Нажмите E, чтобы открыть";

    private bool isOpen = false;
    private bool isLocked = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private float openFraction = 1f;   // 1 - открыта полностью, меньше - приоткрыта (щёлка)

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        if (isOpen)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Slerp(closedRotation, openRotation, openFraction), Time.deltaTime * speed);
        else
            transform.localRotation = Quaternion.Slerp(transform.localRotation, closedRotation, Time.deltaTime * speed);
    }

    public void Interact()
    {
        if (isLocked) return; // Если дверь заперта, игрок ничего не сделает
        isOpen = !isOpen;
        openFraction = 1f;
    }

    // Вот этот метод искал OfficeTrigger!
    public void LockDoor()
    {
        isLocked = true;
        isOpen = false; // Принудительно закрываем дверь
    }

    public void UnlockDoor()
    {
        isLocked = false;
    }

    public bool IsLocked => isLocked;
    public bool IsOpen => isOpen;

    // Для шкафов: открыть/закрыть без переключения
    public void SetOpen(bool open)
    {
        if (!isLocked) { isOpen = open; openFraction = 1f; }
    }

    // Приоткрыть на долю от полного угла (бабка заглядывает в шкаф)
    public void SetAjar(float fraction)
    {
        if (isLocked) return;
        isOpen = true;
        openFraction = Mathf.Clamp01(fraction);
    }
}