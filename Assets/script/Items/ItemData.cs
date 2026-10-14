using UnityEngine;

public enum ItemType
{
    Flashlight,
    Battery,
    Book,
    Stone,
    Lighter,
    Candle,
    LoreNote
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName = "Item";
    public Sprite icon;
    public ItemType type;
    public int maxStack = 1;
    [TextArea] public string description;
}