using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    public Image icon;
    public Text countText;
    public Image highlight; // Желтая рамка выбора

    public void Set(InventorySlot slot)
    {
        if (slot == null || slot.IsEmpty)
        {
            icon.enabled = false;
            countText.text = "";
        }
        else
        {
            icon.enabled = true;
            icon.sprite = slot.item.icon;
            countText.text = slot.amount > 1 ? slot.amount.ToString() : "";
        }
    }

    public void SetHighlight(bool isActive)
    {
        if (highlight != null)
            highlight.gameObject.SetActive(isActive);
    }
}