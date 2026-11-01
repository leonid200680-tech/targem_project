using UnityEngine;

// Передаёт параметры WindZone в шейдер Nature/VegetationWind (деревья и кусты на террейне).
// Висит на объекте с WindZone; работает и в редакторе, чтобы ветер было видно в Scene.
[ExecuteAlways]
[RequireComponent(typeof(WindZone))]
public class VegetationWind : MonoBehaviour
{
    [Tooltip("Общий множитель силы ветра поверх WindZone")]
    public float strengthMultiplier = 1f;

    private WindZone windZone;

    static readonly int WindDirId = Shader.PropertyToID("_VegWindDir");
    static readonly int WindParamsId = Shader.PropertyToID("_VegWindParams");

    void OnEnable()
    {
        windZone = GetComponent<WindZone>();
        RefreshTerrainTrees();
    }

    void Start()
    {
        // Террейн мог включиться позже этого объекта
        RefreshTerrainTrees();
    }

    // Террейн рисует деревья копиями материалов и кэширует их; без обновления
    // изменения шейдера/параметров ветра до деревьев не доходят
    void RefreshTerrainTrees()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain.terrainData == null) continue;
            terrain.terrainData.RefreshPrototypes();
            terrain.Flush();
        }
    }

    void Update()
    {
        Vector3 dir = transform.forward;
        Shader.SetGlobalVector(WindDirId, new Vector4(dir.x, dir.y, dir.z, windZone.windMain * strengthMultiplier));
        Shader.SetGlobalVector(WindParamsId, new Vector4(windZone.windTurbulence, windZone.windPulseMagnitude, windZone.windPulseFrequency, 1f));
    }

    void OnDisable()
    {
        // Без компонента шейдер переходит на значения по умолчанию
        Shader.SetGlobalVector(WindParamsId, Vector4.zero);
    }
}
