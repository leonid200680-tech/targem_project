using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public InventorySlotUI slotPrefab;

    private InventorySlotUI[] slotUIs;

    void Start()
    {
        slotUIs = new InventorySlotUI[Inventory.SIZE];
        for (int i = 0; i < Inventory.SIZE; i++)
        {
            slotUIs[i] = Instantiate(slotPrefab, transform);
        }

        inventory.OnChanged += Refresh;
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < Inventory.SIZE; i++)
            slotUIs[i].Set(inventory.slots[i]);
    }
}