using UnityEngine;

public class LighterInHands : MonoBehaviour
{
    [Header("Ссылки на модель в руках")]
    public GameObject lighterModelInHands; // LighterHolder
    public ItemData lighterItem;

    [Header("Свет и Звук")]
    public Light lighterFlame;             // Объект LighterFlame (Point Light)
    public AudioSource lighterAudioSource; // Audio Source на модели

    private Inventory inventory;
    private InventoryUI inventoryUI;
    private bool isLit = false;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        inventoryUI = FindObjectOfType<InventoryUI>();

        if (lighterModelInHands != null) lighterModelInHands.SetActive(false);
        if (lighterFlame != null) lighterFlame.enabled = false;
    }

    void Update()
    {
        if (inventory == null || inventoryUI == null) return;

        // 1. Есть ли зажигалка в инвентаре?
        bool hasLighter = inventory.Has(lighterItem);

        // 2. Выбрана ли она сейчас в хотбаре?
        bool isSelected = inventoryUI.GetSelectedItem() == lighterItem;

        // 3. Показываем модель в руках
        if (lighterModelInHands != null)
        {
            lighterModelInHands.SetActive(hasLighter && isSelected);
        }

        // Если зажигалку убрали из рук, тушим огонь
        if (!isSelected && isLit)
        {
            TurnOffLighter();
        }

        // 4. Поджигаем по нажатию ЛКМ (только если зажигалка в руках)
        if (isSelected && Input.GetMouseButtonDown(0)) // 0 = Левая кнопка мыши
        {
            ToggleLighter();
        }
    }

    void ToggleLighter()
    {
        isLit = !isLit;

        if (lighterFlame != null) lighterFlame.enabled = isLit;

        if (lighterAudioSource != null && !lighterAudioSource.isPlaying)
        {
            lighterAudioSource.Play();
        }
    }

    void TurnOffLighter()
    {
        isLit = false;
        if (lighterFlame != null) lighterFlame.enabled = false;
    }
}