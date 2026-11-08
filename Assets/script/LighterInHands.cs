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
    private Light beam;   // слабый направленный свет: одной точечной лампы мало, чтобы видеть коридор

    void SetBeam(bool on)
    {
        if (beam == null && on)
        {
            var cam = lighterFlame != null ? lighterFlame.GetComponentInParent<Camera>(true) : null;
            if (cam == null) return;
            var go = new GameObject("Lighter Beam");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0.15f, -0.15f, 0.1f);
            beam = go.AddComponent<Light>();
            go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            beam.type = LightType.Spot;
            beam.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            beam.intensity = 260f;
            beam.range = 9f;
            beam.spotAngle = 80f;
            beam.innerSpotAngle = 25f;
            beam.color = new Color(1f, 0.72f, 0.42f);
            beam.shadows = LightShadows.None;
        }
        if (beam != null) beam.enabled = on;
    }

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
        if (isSelected && BotInput.GetMouseButtonDown(0) && !DialogueManager.IsDialogueActive && !PauseMenu.GameIsPaused)
        {
            ToggleLighter();
        }
    }

    void ToggleLighter()
    {
        isLit = !isLit;

        if (lighterFlame != null) lighterFlame.enabled = isLit;
        SetBeam(isLit);

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
        SetBeam(false);
        if (lighterAudioSource != null) lighterAudioSource.Stop();
    }
}