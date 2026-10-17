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
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectSlot(4);

        // Переключение колесиком мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) SelectSlot((selectedIndex - 1 + Inventory.SIZE) % Inventory.SIZE);
        if (scroll < 0f) SelectSlot((selectedIndex + 1) % Inventory.SIZE);
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