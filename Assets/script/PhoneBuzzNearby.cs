using UnityEngine;

// Телефон сам начинает вибрировать, когда игрок подходит близко (один раз) -
// по звуку его легче найти. Ответить можно по E, как обычно.
[RequireComponent(typeof(PhoneController))]
public class PhoneBuzzNearby : MonoBehaviour
{
    public float triggerDistance = 6f;
    [Tooltip("Не раньше этого шага квеста (чтобы сообщение сестры не попалось раньше звонка мамы)")]
    public QuestManager.Step fromStep = QuestManager.Step.ThirdFloor;

    private PhoneController phone;
    private Transform player;
    private bool done;

    void Start()
    {
        phone = GetComponent<PhoneController>();
    }

    void Update()
    {
        if (done) return;
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
        }

        var qm = QuestManager.Instance;
        if (qm != null && qm.CurrentStep < fromStep) return;

        if (Vector3.Distance(player.position, transform.position) <= triggerDistance)
        {
            done = true;
            phone.StartRinging();
        }
    }
}
