using UnityEngine;

// Телефон сам начинает вибрировать, когда игрок подходит близко (один раз) -
// по звуку его легче найти. Ответить можно по E, как обычно.
[RequireComponent(typeof(PhoneController))]
public class PhoneBuzzNearby : MonoBehaviour
{
    public float triggerDistance = 6f;
    [Tooltip("Звонит сразу, как игрок вошёл в здание: пропустить телефон у стола легко")]
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));
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

        if (buildingZone.Contains(player.position) || Vector3.Distance(player.position, transform.position) <= triggerDistance)
        {
            done = true;
            phone.StartRinging();
        }
    }
}
