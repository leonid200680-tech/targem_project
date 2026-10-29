using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    public string prompt = "Нажмите E, чтобы подобрать";

    public static int stonesCollected = 0;

    public void Pickup(Inventory playerInventory)
    {
        if (playerInventory == null) return;

        if (playerInventory.Add(item, amount))
        {
            if (item.type == ItemType.Stone)
            {
                stonesCollected++;
                HallucinationManager hm = FindObjectOfType<HallucinationManager>();

                if (hm != null)
                {
                    if (stonesCollected == 1)
                    {
                        hm.PlayCreepyNoise();
                    }
                    else if (stonesCollected == 2)
                    {
                        hm.TriggerSecondStoneEvent(); // Вызываем метод с картинкой и бабкой
                    }
                }
            }
            Destroy(gameObject);
        }
    }
}