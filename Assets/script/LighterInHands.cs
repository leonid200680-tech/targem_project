using UnityEngine;

public class LighterInHands : MonoBehaviour
{
    [Header("Ссылки на модель в руках")]
    public GameObject lighterModelInHands; // LighterHolder
    public ItemData lighterItem;
    public InventoryUI inventoryUI; // <--- ДОБАВИЛ: Перетащи сюда UI инвентаря

    [Header("Свет и Звук")]
    public Light lighterFlame;
    public AudioSource lighterAudioSource;

    private bool isLit = false;

    void Start()
    {
        if (lighterModelInHands != null) lighterModelInHands.SetActive(false);
        if (lighterFlame != null) lighterFlame.enabled = false;
    }

    void Update()
    {
        if (inventoryUI == null) return;

        // Проверяем, выбран ли предмет
        bool isSelected = inventoryUI.GetSelectedItem() == lighterItem;

        // Если выбран - включаем, не выбран - выключаем
        if (lighterModelInHands != null)
        {
            if (lighterModelInHands.activeSelf != isSelected)
            {
                lighterModelInHands.SetActive(isSelected);
            }
        }

        // Если убрали из рук, тушим огонь
        if (!isSelected && isLit)
        {
            TurnOffLighter();
        }

        // Поджигаем по нажатию ЛКМ
        if (isSelected && Input.GetMouseButtonDown(0))
        {
            ToggleLighter();
        }
    }

    void ToggleLighter()
    {
        isLit = !isLit;

        if (lighterFlame != null) lighterFlame.enabled = isLit;

        if (lighterAudioSource != null)
        {
            if (isLit) lighterAudioSource.Play();
            else lighterAudioSource.Stop();
        }
    }

    void TurnOffLighter()
    {
        isLit = false;
        if (lighterFlame != null) lighterFlame.enabled = false;
        if (lighterAudioSource != null) lighterAudioSource.Stop();
    }
}