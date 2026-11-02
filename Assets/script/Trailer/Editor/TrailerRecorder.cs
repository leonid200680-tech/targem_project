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

    [MenuItem("Tools/Trailer/Preview (measure audio)")]
    public static void MeterRun()
    {
        EditorPrefs.SetBool(PendingKey + ".Meter", true);
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
            if (s == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PendingKey + ".Meter", false))
            {
                EditorPrefs.SetBool(PendingKey + ".Meter", false);
                EditorApplication.delayCall += PreviewWithMeter;
            }
        };
    }

    public static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer", "Kontora_Trailer"));

    // Прогон без записи с замером громкости (для проверки звука): уровни пишутся в Trailer/levels.txt
    public static bool measureOnly;
    static System.Text.StringBuilder levels;
    static float nextMeasure;

    public static void PreviewWithMeter()
    {
        measureOnly = true;
        StartTrailer();
    }

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
        director.buzz = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/script/Мерцающая лампочка/Лампочка(buzz).mp3");
        director.vibration = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Озвучка/PhoneVibration/Vibration.mp3");
        director.mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/MainMixer.mixer");

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

        if (measureOnly)
        {
            levels = new System.Text.StringBuilder();
            nextMeasure = 0f;
        }
        else
        {
            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
        }
        director.Begin();
        EditorApplication.update += Watch;
        Debug.Log("[Trailer] recording -> " + OutputPath + ".mp4");
    }

    static readonly float[] meterBuf = new float[2048];
    static float peakAcc, rmsAcc; static int rmsCount;

    static void Watch()
    {
        if (measureOnly && levels != null && EditorApplication.isPlaying)
        {
            AudioListener.GetOutputData(meterBuf, 0);
            double s = 0; float pk = 0;
            foreach (float v in meterBuf) { s += v * v; pk = Mathf.Max(pk, Mathf.Abs(v)); }
            rmsAcc += (float)System.Math.Sqrt(s / meterBuf.Length); rmsCount++; peakAcc = Mathf.Max(peakAcc, pk);
            if (Time.time >= nextMeasure)
            {
                float rms = rmsCount > 0 ? rmsAcc / rmsCount : 0f;
                levels.AppendLine($"{Time.time,5:F0}s  rms={rms:F3} ({20 * Mathf.Log10(Mathf.Max(rms, 1e-5f)),6:F1} dB)  peak={peakAcc:F2}");
                rmsAcc = 0; rmsCount = 0; peakAcc = 0; nextMeasure = Time.time + 1f;
            }
        }
        if (director != null && !director.Finished && EditorApplication.isPlaying) return;
        EditorApplication.update -= Watch;
        if (measureOnly && levels != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(OutputPath), "levels.txt"), levels.ToString());
            levels = null; measureOnly = false;
        }
        if (controller != null && controller.IsRecording()) controller.StopRecording();
        controller = null;
        Debug.Log("[Trailer] done: " + OutputPath + ".mp4");
        EditorApplication.isPlaying = false;
    }
}
