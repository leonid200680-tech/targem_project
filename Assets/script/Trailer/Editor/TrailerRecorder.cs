using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Tools > Trailer > Record: запускает игру, проигрывает TrailerDirector и пишет MP4 (1920x1080, 30 к/с, со звуком)
// в папку Trailer/ рядом с проектом.
public static class TrailerRecorder
{
    const string PendingKey = "TrailerRecorder.Pending";
    static RecorderController controller;
    static TrailerDirector director;

    [MenuItem("Tools/Trailer/Record")]
    public static void Record()
    {
        EditorPrefs.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PendingKey, false))
            {
                EditorPrefs.SetBool(PendingKey, false);
                EditorApplication.delayCall += StartTrailer;
            }
        };
    }

    public static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer", "Kontora_Trailer"));

    public static void StartTrailer()
    {
        // Сразу к игре: без заставки и меню
        var splash = GameObject.Find("StudioSplash"); if (splash != null) Object.Destroy(splash);
        var menu = Object.FindAnyObjectByType<MainMenuController>();
        if (menu != null) menu.StartGame();
        GameSettings.SetQuality(0);
        GameSettings.SetBrightness(0f);

        var go = new GameObject("Trailer Director");
        director = go.AddComponent<TrailerDirector>();
        director.titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CormorantSC/CormorantSC-Bold SDF.asset");
        director.titleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/CormorantSC/CormorantSC-Bold Title.mat");
        director.textFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CormorantSC/CormorantSC-Medium SDF.asset");
        director.textMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/CormorantSC/CormorantSC-Medium Shadow.mat");
        director.heartbeat = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/звуки лентяй/stuk_serdca_-_zvuk_serdcebieniya.mp3");
        director.scream = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/звуки лентяй/скример.mp3");
        director.growl = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/monster-growl-and-squelch.mp3");
        director.momLine = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Озвучка/Твоя сестра не отвечает.mp3");
        director.addictLine = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Озвучка 2/4 реплика (наркоша).mp3");

        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        settings.SetRecordModeToManual();
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.FrameRate = 30f;
        settings.CapFrameRate = true;

        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Trailer";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.CaptureAudio = true;
        movie.OutputFile = OutputPath;
        settings.AddRecorderSettings(movie);

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
        director.Begin();
        EditorApplication.update += Watch;
        Debug.Log("[Trailer] recording -> " + OutputPath + ".mp4");
    }

    static void Watch()
    {
        if (director != null && !director.Finished && EditorApplication.isPlaying) return;
        EditorApplication.update -= Watch;
        if (controller != null && controller.IsRecording()) controller.StopRecording();
        controller = null;
        Debug.Log("[Trailer] done: " + OutputPath + ".mp4");
        EditorApplication.isPlaying = false;
    }
}
