using UnityEngine;

public class CandleInteract : MonoBehaviour
{
    [Header("Настройки")]
    public string prompt = "Нажмите E, чтобы зажечь (нужна зажигалка)";
    public GameObject fireEffect; // Объект огня (Particle System или Light)

    public bool isLit = false;
    private Pentagram pentagram;

    void Start()
    {
        pentagram = FindObjectOfType<Pentagram>();
        if (fireEffect != null) fireEffect.SetActive(false);
    }

    public void TryLight(Inventory playerInventory)
    {
        if (isLit) return;

        // Проверяем, есть ли зажигалка в инвентаре
        bool hasLighter = false;
        foreach (var slot in playerInventory.slots)
        {
            if (slot.item != null && slot.item.type == ItemType.Lighter)
            {
                hasLighter = true;
                break;
            }
        }

        if (hasLighter)
        {
            isLit = true;
            if (fireEffect != null) fireEffect.SetActive(true);
            Debug.Log("Свеча зажжена!");

            if (pentagram != null) pentagram.OnCandleLit();
        }
        else
        {
            Debug.Log("Нужна зажигалка, чтобы зажечь свечу!");
        }
    }
}