using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Ссылки")]
    public Light spotLight;
    public ItemData flashlightItem;
    public ItemData batteryItem;
    public InventoryUI inventoryUI; // Ссылка на наш UI
    public GameObject handModel;    // Модель фонарика в руках (если есть)

    [Header("Заряд")]
    public float maxCharge = 60f;
    public float currentCharge = 60f;
    public float batteryRefill = 30f;

    private Inventory inventory;
    private bool isOn = false;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        if (spotLight != null) spotLight.enabled = false;
        if (handModel != null) handModel.SetActive(false);
    }

    void Update()
    {
        if (inventory == null || inventoryUI == null) return;

        // Проверяем: есть ли фонарик в сумке И выбран ли именно он сейчас
        bool isSelected = inventoryUI.GetSelectedItem() == flashlightItem;
        bool hasFlashlight = inventory.Has(flashlightItem);

        // Показываем модель в руках, только если выбран фонарик
        if (handModel != null)
            handModel.SetActive(isSelected);

        // Если переключились на другой предмет, принудительно тушим свет
        if (!isSelected && isOn)
        {
            isOn = false;
            if (spotLight != null) spotLight.enabled = false;
        }

        // Включение по F
        if (Input.GetKeyDown(KeyCode.F) && isSelected && currentCharge > 0)
        {
            isOn = !isOn;
            if (spotLight != null) spotLight.enabled = isOn;
        }

        // Расход заряда
        if (isOn)
        {
            currentCharge -= Time.deltaTime;
            if (currentCharge <= 0)
            {
                currentCharge = 0;
                isOn = false;
                if (spotLight != null) spotLight.enabled = false;
                Debug.Log("Фонарик разрядился!");
            }
        }

        // Зарядка батарейкой
        if (Input.GetKeyDown(KeyCode.R) && isSelected)
        {
            if (inventory.Has(batteryItem) && currentCharge < maxCharge)
            {
                inventory.Remove(batteryItem, 1);
                currentCharge = Mathf.Min(currentCharge + batteryRefill, maxCharge);
                Debug.Log("Фонарик заряжен: " + currentCharge);
            }
        }
    }
    // Публичный метод, чтобы другие скрипты могли проверить наличие фонарика
    public bool HasFlashlightInInventory()
    {
        if (inventory == null) return false;
        return inventory.Has(flashlightItem);
    }
}