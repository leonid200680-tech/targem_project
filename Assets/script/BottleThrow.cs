using System.Collections;
using UnityEngine;

// Бутылка в руке: выбрана в инвентаре - видна в руке, ЛКМ - замах и бросок.
// Брошенная бутылка летит по дуге, кувыркаясь, и разбивается (BottleProjectile).
public class BottleThrow : MonoBehaviour
{
    public ItemData bottleItem;
    public InventoryUI inventoryUI;
    public GameObject holder;            // BottleHolder под камерой (модель в руке)
    public BottleProjectile projectilePrefab;
    public float throwSpeed = 15f;
    public float upward = 2.5f;

    private Inventory inventory;
    private Transform cam;
    private bool throwing;
    private Vector3 restPos;
    private Quaternion restRot;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        var c = GetComponentInChildren<Camera>();
        cam = c != null ? c.transform : transform;
        if (holder != null)
        {
            restPos = holder.transform.localPosition;
            restRot = holder.transform.localRotation;
            holder.SetActive(false);
        }
    }

    void Update()
    {
        if (inventoryUI == null || holder == null) return;
        bool selected = inventoryUI.GetSelectedItem() == bottleItem && inventory.Has(bottleItem);
        if (!throwing && holder.activeSelf != selected) holder.SetActive(selected);

        var pc = GetComponent<PlayerController>();
        bool canAct = pc != null && pc.enabled && !PauseMenu.GameIsPaused && !DialogueManager.IsDialogueActive;
        if (selected && !throwing && canAct && BotInput.GetMouseButtonDown(0)) StartCoroutine(Throw());
    }

    IEnumerator Throw()
    {
        throwing = true;
        Transform h = holder.transform;
        // Камера рядом с holder задаёт масштаб: смещения в метрах переводим в её локальные единицы
        Vector3 s = h.parent != null ? h.parent.lossyScale : Vector3.one;
        Vector3 M(Vector3 m) => new Vector3(m.x / s.x, m.y / s.y, m.z / s.z);

        // Замах: рука уходит назад и вверх, бутылка заваливается назад
        Vector3 back = restPos + M(new Vector3(0.06f, 0.14f, -0.16f));
        Quaternion backRot = restRot * Quaternion.Euler(-55f, 0f, 10f);
        yield return Move(h, restPos, restRot, back, backRot, 0.22f, true);

        // Бросок: резко вперёд
        Vector3 fwd = restPos + M(new Vector3(-0.04f, 0.02f, 0.22f));
        Quaternion fwdRot = restRot * Quaternion.Euler(40f, 0f, -8f);
        yield return Move(h, back, backRot, fwd, fwdRot, 0.08f, false);

        // Бутылка вылетает из руки
        var model = holder.GetComponentInChildren<MeshRenderer>();
        Vector3 from = model != null ? model.bounds.center : h.position;
        if (projectilePrefab != null)
        {
            var p = Instantiate(projectilePrefab, from, Quaternion.LookRotation(cam.forward) * Quaternion.Euler(90f, 0f, 0f));
            var rb = p.GetComponent<Rigidbody>();
            rb.linearVelocity = cam.forward * throwSpeed + Vector3.up * upward;
            rb.angularVelocity = cam.right * Random.Range(10f, 16f) + Random.insideUnitSphere * 3f;
            foreach (var col in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(p.GetComponent<Collider>(), col);
        }
        inventory.Remove(bottleItem, 1);
        holder.SetActive(false);

        // Рука возвращается на место (уже без бутылки)
        yield return new WaitForSeconds(0.25f);
        h.localPosition = restPos;
        h.localRotation = restRot;
        throwing = false;
    }

    static IEnumerator Move(Transform t, Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float time, bool easeOut)
    {
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = e / time;
            k = easeOut ? 1f - (1f - k) * (1f - k) : k * k;
            t.localPosition = Vector3.LerpUnclamped(p0, p1, k);
            t.localRotation = Quaternion.SlerpUnclamped(r0, r1, k);
            yield return null;
        }
        t.localPosition = p1;
        t.localRotation = r1;
    }
}
