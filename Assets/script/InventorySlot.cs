using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}