using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

// Tools > VR > Симулятор шлема: включает Mock Runtime из OpenXR, чтобы проверять VR в редакторе без шлема
// (управление - в VRSimulator). Перед сборкой билда симулятор выключается сам,
// иначе на настоящем шлеме игра искала бы фальшивый рантайм вместо SteamVR / Meta Link.
public class VRSimulatorMenu : IPreprocessBuildWithReport
{
    const string MenuPath = "Tools/VR/Симулятор шлема";

    static OpenXRFeature MockFeature()
    {
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if (settings == null) return null;
        foreach (var f in settings.GetFeatures())
            if (f.GetType().Name == "MockRuntime") return f;
        return null;
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        var f = MockFeature();
        if (f == null) { Debug.LogError("[VR] Mock Runtime не найден в настройках OpenXR"); return; }
        if (EditorApplication.isPlaying) { Debug.LogWarning("[VR] Выйди из Play Mode, потом переключай симулятор"); return; }
        f.enabled = !f.enabled;
        EditorUtility.SetDirty(f);
        AssetDatabase.SaveAssets();
        Debug.Log(f.enabled
            ? "[VR] Симулятор шлема включён: нажми Play. ПКМ+мышь - голова, ЛКМ - курок"
            : "[VR] Симулятор шлема выключен");
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        var f = MockFeature();
        Menu.SetChecked(MenuPath, f != null && f.enabled);
        return f != null;
    }

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var f = MockFeature();
        if (f == null || !f.enabled) return;
        f.enabled = false;
        EditorUtility.SetDirty(f);
        AssetDatabase.SaveAssets();
        Debug.Log("[VR] Симулятор шлема выключен перед сборкой билда");
    }
}
