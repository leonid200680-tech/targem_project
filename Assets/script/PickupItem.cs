using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [Header("Настройки предмета")]
    public ItemData item;
    public int amount = 1;
    public string prompt = "Нажмите E, чтобы подобрать";

    // Метод для взаимодействия (вызывается из PlayerInteraction)
    public void Pickup(Inventory playerInventory)
    {
        if (playerInventory == null) return;

        // Пытаемся добавить предмет в инвентарь
        if (playerInventory.Add(item, amount))
        {
            // Если это фонарик — обновляем квест!
            if (item.type == ItemType.Flashlight && QuestManager.Instance != null)
            {
                QuestManager.Instance.SetQuestStep(1); // Шаг 1: Проверить телек
            }

            // Уничтожаем объект в мире (предмет подобран)
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Инвентарь полон! Не могу подобрать предмет.");
        }
    }
}