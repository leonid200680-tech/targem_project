using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    public string prompt = "Нажмите E, чтобы подобрать";

    // Этот метод будет вызывать игрок, когда нажмет E
    public void Pickup(Inventory playerInventory)
    {
        if (playerInventory.Add(item, amount))
        {
            // Если предмет успешно добавлен в инвентарь, удаляем его из мира
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Инвентарь полон!");
        }
    }
}