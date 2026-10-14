using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;

    private bool playerInRange = false;
    private Inventory playerInventory;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[Pickup] Нажал E. Inventory = " + playerInventory);

            if (playerInventory == null)
            {
                Debug.LogError("[Pickup] Inventory не найден на Player!");
                return;
            }

            bool ok = playerInventory.Add(item, amount);
            Debug.Log("[Pickup] Add вернул: " + ok);

            if (ok) Destroy(gameObject);
            else Debug.Log("Инвентарь полон!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("[Pickup] Вошёл объект: " + other.name + " | тег: " + other.tag);
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerInventory = other.GetComponent<Inventory>();
            Debug.Log("[Pickup] Player в зоне. Inventory найден: " + (playerInventory != null));
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerInventory = null;
        }
    }
}