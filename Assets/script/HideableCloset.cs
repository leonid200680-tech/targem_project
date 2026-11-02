using System.Collections;
using UnityEngine;

// Шкаф-укрытие. По E (на дверцу или на сам шкаф) дверцы открываются, игрок залезает внутрь, дверцы закрываются.
// Изнутри по E - дверцы открываются, игрок выходит. Пока игрок внутри, бабка его не видит и не может схватить.
// Точки "внутри" и "выход" создаются сами по габаритам шкафа, если не заданы вручную.
public class HideableCloset : MonoBehaviour
{
    [Header("Настройки")]
    public Transform hidePoint; // Точка внутри шкафа
    public Transform exitPoint; // Точка, куда игрок выйдет (перед шкафом)
    public string prompt = "Нажмите E, чтобы спрятаться";
    public float eyeHeight = 1.4f;   // высота корня игрока над полом шкафа (как у HidePoint, расставленного вручную)
    [Tooltip("Изнутри дверцы прозрачные (у модели одна сторона), поэтому на них изнутри ставится панель со щелями")]
    public Material insideSlatsMaterial;

    public static bool IsPlayerHidden = false; // Глобальный флаг: игрок в укрытии

    private bool isPlayerInside = false;
    private bool moving = false;
    private int enteredFrame = -1;
    private GameObject playerObj;
    private PlayerController pc;
    private InteractableDoor[] doors;
    private GameObject slats;

    void Start()
    {
        playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) pc = playerObj.GetComponent<PlayerController>();
        doors = GetComponentsInChildren<InteractableDoor>(true);
        BuildPoints();
    }

    // Внутренняя точка - центр шкафа, лицом к дверцам; выход - перед дверцами
    void BuildPoints()
    {
        var body = transform.Find("Cube");
        var r = body != null ? body.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
        if (r == null) return;
        Bounds b = r.bounds;

        Vector3 front = Vector3.zero;
        foreach (var d in doors)
        {
            var dr = d.GetComponent<Renderer>();
            front += (dr != null ? dr.bounds.center : d.transform.position) - b.center;
        }
        front.y = 0f;
        if (front.sqrMagnitude < 1e-4f) front = transform.forward;
        front.Normalize();

        float floor = b.min.y;
        if (Physics.Raycast(b.center, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point.y;
        float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z))));

        // Панель со щелями изнутри на дверцах: сквозь щели видно комнату, как в хоррорах
        if (insideSlatsMaterial != null && transform.Find("Inside Slats") == null && doors.Length > 0)
        {
            Bounds db = doors[0].GetComponent<Renderer>().bounds;
            foreach (var d in doors) db.Encapsulate(d.GetComponent<Renderer>().bounds);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Inside Slats";
            q.transform.SetParent(transform, true);
            Vector3 c = db.center - front * (Mathf.Abs(Vector3.Dot(db.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z)))) + 0.02f);
            q.transform.SetPositionAndRotation(c, Quaternion.LookRotation(front));   // лицом внутрь шкафа (квад виден со стороны -forward)
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float width = Mathf.Abs(Vector3.Dot(db.size, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));
            q.transform.localScale = new Vector3(width, db.size.y, 1f);
            var qr = q.GetComponent<MeshRenderer>();
            qr.sharedMaterial = insideSlatsMaterial;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.SetActive(false);   // видна только пока игрок внутри
            slats = q;
        }

        if (hidePoint == null)
        {
            hidePoint = new GameObject("HidePoint").transform;
            hidePoint.SetParent(transform, true);
            hidePoint.SetPositionAndRotation(new Vector3(b.center.x, floor + eyeHeight, b.center.z), Quaternion.LookRotation(front));
        }
        if (exitPoint == null)
        {
            exitPoint = new GameObject("exit point").transform;
            exitPoint.SetParent(transform, true);
            Vector3 basePos = new Vector3(b.center.x, floor + eyeHeight, b.center.z);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Vector3 p = basePos + front * (depth + 0.75f);
            // Перед шкафом может стоять мебель: ищем свободное место чуть дальше или сбоку
            foreach (float dist in new[] { 0.75f, 1.0f, 1.3f })
            {
                bool found = false;
                foreach (float s in new[] { 0f, 0.5f, -0.5f, 0.9f, -0.9f })
                {
                    Vector3 q = basePos + front * (depth + dist) + side * s;
                    if (!Physics.CheckCapsule(q + Vector3.down * 0.9f, q + Vector3.up * 0.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    { p = q; found = true; break; }
                }
                if (found) break;
            }
            exitPoint.SetPositionAndRotation(p, Quaternion.LookRotation(front));
        }
    }

    void Update()
    {
        // Выход из шкафа (не в тот же кадр, когда залезли)
        if (isPlayerInside && !moving && Time.frameCount != enteredFrame && Input.GetKeyDown(KeyCode.E))
            StartCoroutine(ExitRoutine());
    }

    public void Interact()
    {
        if (!isPlayerInside && !moving && !IsPlayerHidden) StartCoroutine(EnterRoutine());
    }

    void SetDoors(bool open)
    {
        foreach (var d in doors) d.SetOpen(open);
    }

    IEnumerator EnterRoutine()
    {
        // Игрок выключен, пока открыто главное меню, поэтому ищем его здесь, а не в Start
        if (pc == null)
        {
            pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerObj = pc.gameObject;
        }
        if (pc == null || hidePoint == null) yield break;
        moving = true;
        isPlayerInside = true;
        enteredFrame = Time.frameCount;
        pc.enabled = false;
        SetDoors(true);
        yield return new WaitForSeconds(0.35f);

        IsPlayerHidden = true; // Игрок невидим для бабки
        Teleport(hidePoint);
        yield return new WaitForSeconds(0.25f);
        SetDoors(false);
        yield return new WaitForSeconds(0.2f);
        if (slats != null) slats.SetActive(true);
        yield return new WaitForSeconds(0.3f);
        moving = false;
    }

    IEnumerator ExitRoutine()
    {
        moving = true;
        if (slats != null) slats.SetActive(false);
        SetDoors(true);
        yield return new WaitForSeconds(0.35f);

        IsPlayerHidden = false; // Игрок снова видим
        if (exitPoint != null) Teleport(exitPoint);
        isPlayerInside = false;
        if (pc != null) pc.enabled = true;
        yield return new WaitForSeconds(0.5f);
        SetDoors(false);
        moving = false;
    }

    void Teleport(Transform point)
    {
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerObj.transform.SetPositionAndRotation(point.position, point.rotation);
        if (cc != null) cc.enabled = true;
    }

    void OnDisable()
    {
        if (isPlayerInside) IsPlayerHidden = false;
    }
}
