using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Tools > Trailer > Record Gameplay: запускает игру, бот проходит её от меню до концовки (GameplayDirector),
// пишется MP4 1920x1080, 30 к/с, со звуком в Trailer/Kontora_Gameplay.mp4.
// Preview Gameplay - тот же прогон без записи (для проверки маршрута), ход событий - в Trailer/gameplay_log.txt.
public static class GameplayRecorder
{
    const string PendingKey = "GameplayRecorder.Pending";
    static RecorderController controller;
    static GameplayDirector director;
    static bool record;

    public static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer", "Kontora_Gameplay"));
    static string LogPath => Path.Combine(Path.GetDirectoryName(OutputPath), "gameplay_log.txt");

    [MenuItem("Tools/Trailer/Record Gameplay")]
    public static void Record() => Launch(true);

    [MenuItem("Tools/Trailer/Preview Gameplay (no recording)")]
    public static void Preview() => Launch(false);

    static void Launch(bool rec)
    {
        EditorPrefs.SetInt(PendingKey, rec ? 2 : 1);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            int mode = EditorPrefs.GetInt(PendingKey, 0);
            if (mode == 0) return;
            EditorPrefs.SetInt(PendingKey, 0);
            record = mode == 2;
            EditorApplication.delayCall += Start;
        };
    }

    public static bool IsRunning => director != null && !director.Finished;
    public static string LogText => director != null ? director.Log.ToString() : "";

    static void Start()
    {

        var go = new GameObject("Gameplay Director");
        director = go.AddComponent<GameplayDirector>();
        director.titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CormorantSC/CormorantSC-Bold SDF.asset");
        director.titleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/CormorantSC/CormorantSC-Bold Title.mat");
        director.textFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CormorantSC/CormorantSC-Medium SDF.asset");
        director.textMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/CormorantSC/CormorantSC-Medium Shadow.mat");

        if (record)
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = 30f;
            settings.CapFrameRate = true;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Gameplay";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.CaptureAudio = true;
            movie.OutputFile = OutputPath;
            settings.AddRecorderSettings(movie);

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            Debug.Log("[Gameplay] recording -> " + OutputPath + ".mp4");
        }
        else Time.timeScale = EditorPrefs.GetFloat("GameplayRecorder.Speed", 1f);   // прогон без записи можно ускорить
        GameplayDirector.SkipTo = record ? 0 : EditorPrefs.GetInt("GameplayRecorder.Skip", 0);
        director.Begin();
        EditorApplication.update += Watch;
    }

    static void Watch()
    {
        if (director != null && !director.Finished && EditorApplication.isPlaying) return;
        EditorApplication.update -= Watch;
        if (controller != null && controller.IsRecording()) controller.StopRecording();
        controller = null;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        if (director != null) File.WriteAllText(LogPath, director.Log.ToString());
        Debug.Log("[Gameplay] done" + (record ? ": " + OutputPath + ".mp4" : ""));
        EditorApplication.isPlaying = false;
    }
}
