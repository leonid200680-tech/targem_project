using UnityEngine;
using UnityEngine.UI;

public class BatteryUI : MonoBehaviour
{
    public Slider slider;
    public Flashlight flashlight;
    public InventoryUI inventoryUI; // Добавляем ссылку на UI инвентаря

    void Update()
    {
        if (slider == null || flashlight == null || inventoryUI == null) return;

        // Проверяем: выбран ли сейчас фонарик в хотбаре?
        bool isFlashlightSelected = inventoryUI.GetSelectedItem() == flashlight.flashlightItem;

        // Включаем/выключаем весь объект, на котором висит этот скрипт (или саму полоску)
        if (slider.gameObject.activeSelf != isFlashlightSelected)
        {
            slider.gameObject.SetActive(isFlashlightSelected);
        }

        // Если фонарик выбран, обновляем полоску заряда
        if (isFlashlightSelected)
        {
            float fillAmount = flashlight.currentCharge / flashlight.maxCharge;
            slider.value = Mathf.Lerp(slider.value, fillAmount, Time.deltaTime * 10f);
        }
    }
}