using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Достаёт кадры из записанного трейлера и склеивает раскадровку (для проверки монтажа).
public static class TrailerContactSheet
{
    public static string Status = "";

    public static void Run(string mp4, string outPng, int cols, int rows, float step)
    {
        Status = "running";
        QuestManager.Instance.StartCoroutine(Go(mp4, outPng, cols, rows, step));
    }

    static IEnumerator Go(string mp4, string outPng, int cols, int rows, float step)
    {
        var go = new GameObject("__ContactSheet");
        var vp = go.AddComponent<VideoPlayer>();
        vp.url = mp4; vp.audioOutputMode = VideoAudioOutputMode.None; vp.playOnAwake = false;
        vp.renderMode = VideoRenderMode.RenderTexture;
        var rt = new RenderTexture(480, 270, 0); vp.targetTexture = rt;
        vp.Prepare();
        while (!vp.isPrepared) yield return null;
        double length = vp.length;
        int W = 480, H = 270;
        var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGB24, false);
        var tmp = new Texture2D(W, H, TextureFormat.RGB24, false);
        int i = 0;
        for (double t = 0.5; t < length && i < cols * rows; t += step, i++)
        {
            vp.time = t; vp.Play();
            float wait = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < wait) yield return null;
            vp.Pause();
            yield return null;
            RenderTexture.active = rt; tmp.ReadPixels(new Rect(0, 0, W, H), 0, 0); tmp.Apply(); RenderTexture.active = null;
            int cx = i % cols, cy = rows - 1 - i / cols;
            sheet.SetPixels(cx * W, cy * H, W, H, tmp.GetPixels());
        }
        sheet.Apply();
        System.IO.File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        Status = "done length=" + length.ToString("F1") + "s frames=" + i;
        Object.Destroy(go);
    }
}
