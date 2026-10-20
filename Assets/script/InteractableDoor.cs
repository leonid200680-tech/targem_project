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

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        if (isOpen)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, openRotation, Time.deltaTime * speed);
        else
            transform.localRotation = Quaternion.Slerp(transform.localRotation, closedRotation, Time.deltaTime * speed);
    }

    public void Interact()
    {
        if (isLocked) return; // Если дверь заперта, игрок ничего не сделает
        isOpen = !isOpen;
    }

    // Вот этот метод искал OfficeTrigger!
    public void LockDoor()
    {
        isLocked = true;
        isOpen = false; // Принудительно закрываем дверь
    }
}