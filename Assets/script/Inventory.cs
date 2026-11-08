using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public const int SIZE = 5;
    public InventorySlot[] slots = new InventorySlot[SIZE];

    public event Action OnChanged;

    void Awake()
    {
        for (int i = 0; i < SIZE; i++)
            slots[i] = new InventorySlot();
    }

    public bool Add(ItemData item, int amount = 1)
    {
        // Сначала пытаемся доложить в существующий стак
        for (int i = 0; i < SIZE; i++)
        {
            if (slots[i].item == item && slots[i].amount < item.maxStack)
            {
                slots[i].amount += amount;
                OnChanged?.Invoke();
                return true;
            }
        }
        // Иначе — в пустой слот
        for (int i = 0; i < SIZE; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].item = item;
                slots[i].amount = amount;
                OnChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    public bool CanAdd(ItemData item)
    {
        foreach (var s in slots)
            if (s.IsEmpty || (s.item == item && s.amount < item.maxStack)) return true;
        return false;
    }

    public bool Remove(ItemData item, int amount = 1)
    {
        for (int i = 0; i < SIZE; i++)
        {
            if (slots[i].item == item)
            {
                slots[i].amount -= amount;
                if (slots[i].amount <= 0) slots[i].Clear();
                OnChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    public int Count(ItemData item)
    {
        int c = 0;
        foreach (var s in slots)
            if (s.item == item) c += s.amount;
        return c;
    }

    public bool Has(ItemData item) => Count(item) > 0;
}