using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Tools > Build > Windows: сборка игры для ПК в папку Builds/KONTORA рядом с проектом.
// Итог сборки пишется в Builds/build_result.txt.
public static class KontoraBuild
{
    public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "KONTORA"));
    public static string ResultFile => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "build_result.txt"));

    [MenuItem("Tools/Build/Windows")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory(OutDir);
        if (File.Exists(ResultFile)) File.Delete(ResultFile);

        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = Path.Combine(OutDir, "КОНТОРА.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var sum = report.summary;
        string text = $"result={sum.result}\nerrors={sum.totalErrors} warnings={sum.totalWarnings}\nsize={sum.totalSize / (1024 * 1024)}MB\ntime={sum.totalTime}\npath={sum.outputPath}\n";
        foreach (var step in report.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception) text += "ERROR: " + m.content + "\n";
        File.WriteAllText(ResultFile, text);
        Debug.Log("[Build] " + text);
    }

    public static void BuildWindowsDeferred() => EditorApplication.delayCall += BuildWindows;
}
