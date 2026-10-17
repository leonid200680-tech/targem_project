using UnityEngine;

public class FlashlightSystem : MonoBehaviour
{
    [Header("Настройки фонарика")]
    public ItemData flashlightItem;       // Ассет фонарика из папки Items
    public GameObject handModel;          // Модель фонарика в руках (дочерняя камеры)
    public Light spotLight;               // Источник света (Spot Light)

    [Header("Заряд (по желанию)")]
    public float maxCharge = 100f;
    public float currentCharge = 100f;

    private Inventory inventory;
    private bool isOn = false;

    void Start()
    {
        inventory = GetComponent<Inventory>();

        // На старте всё выключено
        if (handModel != null) handModel.SetActive(false);
        if (spotLight != null) spotLight.enabled = false;
    }

    void Update()
    {
        if (inventory == null) return;

        // 1. Проверяем, подобрали ли мы фонарик
        bool hasFlashlight = inventory.Has(flashlightItem);

        // 2. Включаем/выключаем отображение модели в руках
        if (handModel != null)
            handModel.SetActive(hasFlashlight);

        // 3. Управление светом по кнопке F
        if (Input.GetKeyDown(KeyCode.F) && hasFlashlight)
        {
            if (currentCharge > 0)
            {
                isOn = !isOn; // Переключаем состояние
                if (spotLight != null) spotLight.enabled = isOn;
            }
        }

        // 4. Тратим заряд, если свет горит
        if (isOn && currentCharge > 0)
        {
            currentCharge -= Time.deltaTime * 1.5f; // Скорость разряда
            if (currentCharge <= 0)
            {
                currentCharge = 0;
                isOn = false;
                if (spotLight != null) spotLight.enabled = false;
                Debug.Log("Фонарик сел!");
            }
        }
    }
}