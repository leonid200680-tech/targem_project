using UnityEngine;

public class CigarettePack : MonoBehaviour
{
    public string prompt = "Нажмите E, чтобы закурить (нужна зажигалка)";

    public void Interact(Inventory playerInventory)
    {
        // Проверяем, есть ли зажигалка в инвентаре (используем наш тип Lighter)
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
            Debug.Log("Игрок закурил сигарету! (Здесь можно добавить дым, звук и т.д.)");
            // Здесь в будущем можно добавить эффект дыма (Particle System) у камеры
        }
        else
        {
            Debug.Log("Нужна зажигалка, чтобы закурить!");
        }
    }
}