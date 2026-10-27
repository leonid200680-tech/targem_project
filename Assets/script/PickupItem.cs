using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    public string prompt = "Нажмите E, чтобы подобрать";

    // Ссылка на объект бабки, чтобы включить её
    public GameObject ghostToSpawn;
    private static bool ghostActivated = false; // Защита, чтобы она включилась только 1 раз

    public void Pickup(Inventory playerInventory)
    {
        if (playerInventory == null) return;

        if (playerInventory.Add(item, amount))
        {
            // Если подобрали фонарик — обновляем квест
            if (item.type == ItemType.Flashlight && QuestManager.Instance != null)
            {
                QuestManager.Instance.SetQuestStep(1);
            }

            // ПРОВЕРКА: Если подобрали камень и бабка ещё не включена
            if (item.type == ItemType.Stone && !ghostActivated)
            {
                ActivateGhost();
            }

            Destroy(gameObject);
        }
    }

    void ActivateGhost()
    {
        ghostActivated = true;

        // Ищем бабку, если ссылка не привязана вручную
        if (ghostToSpawn == null)
        {
            GhostChase ghost = FindObjectOfType<GhostChase>(true); // true - ищем даже выключенные
            if (ghost != null) ghostToSpawn = ghost.gameObject;
        }

        if (ghostToSpawn != null)
        {
            ghostToSpawn.SetActive(true);
            Debug.Log("Бабка активирована! Игрок подобрал первый камень.");
        }
    }
}