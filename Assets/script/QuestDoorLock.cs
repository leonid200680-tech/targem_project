using UnityEngine;

// Держит дверь запертой, пока квест не дойдёт до нужного шага
// (дверь вагончика: сначала звонок, телевизор и предметы).
[RequireComponent(typeof(InteractableDoor))]
public class QuestDoorLock : MonoBehaviour
{
    public QuestManager.Step unlockAtStep = QuestManager.Step.ThirdFloor;
    public string lockedPrompt = "Заперто. Сначала закончи дела в вагончике";
    public AudioClip lockedSound;

    private InteractableDoor door;
    private string openPrompt;
    private bool locked;

    void Start()
    {
        door = GetComponent<InteractableDoor>();
        openPrompt = door.prompt;
        door.LockDoor();
        door.prompt = lockedPrompt;
        locked = true;
    }

    void Update()
    {
        if (!locked) return;

        var qm = QuestManager.Instance;
        if (qm != null && qm.CurrentStep >= unlockAtStep)
        {
            locked = false;
            door.UnlockDoor();
            door.prompt = openPrompt;
            enabled = false;
            return;
        }

        // Звук дёрганья ручки, если игрок пытается открыть запертую дверь
        if (lockedSound != null && BotInput.GetKeyDown(KeyCode.E) && IsLookedAt())
            AudioSource.PlayClipAtPoint(lockedSound, transform.position);
    }

    bool IsLookedAt()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        return Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 3f)
            && hit.collider.transform.IsChildOf(transform);
    }
}
