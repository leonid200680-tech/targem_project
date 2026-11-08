using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public InventorySlotUI slotPrefab;

    private InventorySlotUI[] slotUIs;
    private int selectedIndex = 0; // Текущий выбранный слот

    void Start()
    {
        slotUIs = new InventorySlotUI[Inventory.SIZE];
        for (int i = 0; i < Inventory.SIZE; i++)
        {
            slotUIs[i] = Instantiate(slotPrefab, transform);
        }

        inventory.OnChanged += Refresh;
        Refresh();
        SelectSlot(0); // По умолчанию выбран первый слот
    }

    void Update()
    {
        // Переключение цифрами 1-5
        if (BotInput.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (BotInput.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (BotInput.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (BotInput.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
        if (BotInput.GetKeyDown(KeyCode.Alpha5)) SelectSlot(4);

        // Переключение колесиком мыши
        float scroll = BotInput.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) SelectSlot((selectedIndex - 1 + Inventory.SIZE) % Inventory.SIZE);
        if (scroll < 0f) SelectSlot((selectedIndex + 1) % Inventory.SIZE);

        if (BotInput.GetKeyDown(KeyCode.G) && !DialogueManager.IsDialogueActive && !PauseMenu.GameIsPaused
            && !HideableCloset.IsPlayerHidden && !EvacuationPlan.IsOpen) DropSelected();
    }

    // Предмет падает на пол перед игроком
    void DropSelected()
    {
        var item = GetSelectedItem();
        var cam = Camera.main;
        if (item == null || cam == null) return;
        var tv = FindAnyObjectByType<InteractableTV>();
        if (tv != null && tv.IsViewing) return;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        float reach = Physics.Raycast(cam.transform.position, fwd, out var wall, 1.1f, ~0, QueryTriggerInteraction.Ignore) ? wall.distance - 0.2f : 0.9f;
        Vector3 from = cam.transform.position + fwd * Mathf.Max(0.3f, reach);
        // Ближайшая поверхность внизу, кроме самого игрока: иначе предмет повисал на его коллайдере
        float nearest = float.MaxValue;
        Vector3 ground = default;
        foreach (var hit in Physics.RaycastAll(from, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.distance < nearest && !hit.collider.transform.IsChildOf(inventory.transform)) { nearest = hit.distance; ground = hit.point; }
        if (nearest == float.MaxValue) return;
        Vector3 hand = cam.transform.position + cam.transform.forward * 0.35f - cam.transform.up * 0.3f;
        if (PickupItem.Drop(item, ground, hand)) inventory.Remove(item, 1);
    }

    void SelectSlot(int index)
    {
        if (slotUIs == null || slotUIs.Length == 0) return;

        // Убираем подсветку со старого
        if (slotUIs[selectedIndex] != null)
            slotUIs[selectedIndex].SetHighlight(false);

        selectedIndex = index;

        // Включаем подсветку на новом
        if (slotUIs[selectedIndex] != null)
            slotUIs[selectedIndex].SetHighlight(true);
    }

    void Refresh()
    {
        for (int i = 0; i < Inventory.SIZE; i++)
            slotUIs[i].Set(inventory.slots[i]);
    }

    // Метод, чтобы фонарик знал, выбран ли он
    public ItemData GetSelectedItem()
    {
        if (inventory == null || slotUIs == null) return null;
        return inventory.slots[selectedIndex].item;
    }
}