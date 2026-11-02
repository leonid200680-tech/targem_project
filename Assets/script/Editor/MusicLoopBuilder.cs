using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Audio > Build Game Music Loop: из короткого трека игры (27 сек, обрывается на конце)
// собирает длинную версию ~2 минуты с вариациями и плавной склейкой конца с началом.
// Части трека играются по очереди с перекрёстными затуханиями: оригинал, ниже и темнее,
// "издалека" (приглушённо), снова оригинал со сдвигом, совсем низко. Результат - WAV рядом с исходником.
public static class MusicLoopBuilder
{
    const string Source = "Assets/Озвучка/GameMusic/GameMusic.mp3";
    const string Output = "Assets/Озвучка/GameMusic/GameMusicLoop.wav";
    const float Crossfade = 5f;

    struct Part
    {
        public float start, length, pitch, gain, lowpass;   // lowpass: 0 - без фильтра, иначе частота среза
        public Part(float start, float length, float pitch, float gain, float lowpass)
        { this.start = start; this.length = length; this.pitch = pitch; this.gain = gain; this.lowpass = lowpass; }
    }

    [MenuItem("Tools/Audio/Build Game Music Loop")]
    public static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Source);
        clip.LoadAudioData();
        int ch = clip.channels, rate = clip.frequency;
        var src = new float[clip.samples * ch];
        clip.GetData(src, 0);
        float len = clip.length;

        var parts = new[]
        {
            new Part(0f, len, 1f, 1f, 0f),
            new Part(3f, len - 3f, 0.94f, 0.9f, 2600f),     // ниже и темнее
            new Part(0f, len, 1f, 0.6f, 900f),              // как будто из-за стены
            new Part(8f, len - 8f, 1f, 0.95f, 0f),          // оригинал со сдвигом
            new Part(0f, len, 0.89f, 0.8f, 1800f),          // совсем низко
        };

        // Длина результата: части накладываются друг на друга на Crossfade секунд,
        // последний хвост уходит в начало (бесшовный луп)
        float total = 0f;
        foreach (var p in parts) total += p.length / p.pitch - Crossfade;
        int outN = Mathf.RoundToInt(total * rate);
        var mix = new float[outN * ch];
        int fadeN = Mathf.RoundToInt(Crossfade * rate);

        int at = 0;
        foreach (var p in parts)
        {
            int n = Mathf.RoundToInt(p.length / p.pitch * rate);
            var lp = new float[ch];
            float a = p.lowpass > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * p.lowpass / rate) : 1f;
            for (int i = 0; i < n; i++)
            {
                // Плавные вход и выход (равная мощность)
                float g = p.gain;
                if (i < fadeN) g *= Mathf.Sin(i / (float)fadeN * Mathf.PI * 0.5f);
                if (i > n - fadeN) g *= Mathf.Sin((n - i) / (float)fadeN * Mathf.PI * 0.5f);

                // Ресемплинг (смена высоты) с линейной интерполяцией
                float pos = p.start * rate + i * p.pitch;
                int i0 = (int)pos;
                float fr = pos - i0;
                int i1 = Mathf.Min(i0 + 1, clip.samples - 1);
                i0 = Mathf.Min(i0, clip.samples - 1);

                int o = (at + i) % outN;   // хвост последней части заворачивается в начало
                for (int c = 0; c < ch; c++)
                {
                    float s = Mathf.Lerp(src[i0 * ch + c], src[i1 * ch + c], fr);
                    lp[c] += (s - lp[c]) * a;
                    mix[o * ch + c] += lp[c] * g;
                }
            }
            at += n - fadeN;
        }

        float peak = 0f;
        foreach (float s in mix) peak = Mathf.Max(peak, Mathf.Abs(s));
        float norm = peak > 0.95f ? 0.95f / peak : 1f;

        WriteWav(Output, mix, ch, rate, norm);
        AssetDatabase.ImportAsset(Output);

        var imp = (AudioImporter)AssetImporter.GetAtPath(Output);
        var settings = imp.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        imp.defaultSampleSettings = settings;
        imp.SaveAndReimport();
        Debug.Log($"[Music] {Output}: {total:F1} s");
    }

    static void WriteWav(string path, float[] data, int ch, int rate, float gain)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = data.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float s in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * gain * 32767f), -32768, 32767));
        }
    }
}
