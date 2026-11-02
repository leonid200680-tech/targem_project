using UnityEngine;

// Звуки для трейлера, синтезированные в коде: пианино, хоровой пад, "брам" (низкий медный аккорд),
// нарастание перед скримером, свист склейки, суб-падение, пульс, ветер, гул помещения, дыхание, скрип.
public static class TrailerScore
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

    // ---------- Музыка ----------

    // Одинокая нота расстроенного пианино с длинным хвостом
    public static AudioClip Piano(int semi, float length = 4f, float detune = 0.004f)
    {
        int n = (int)(Rate * length); var d = new float[n];
        float f = Note(semi);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 1.4f);
            float s = 0f;
            for (int h = 1; h <= 6; h++)
            {
                float fh = f * h * (1f + detune * (h % 2 == 0 ? 1 : -1));
                s += Mathf.Sin(TwoPi * fh * t) * Mathf.Exp(-t * (0.8f + h * 0.9f)) / (h * h * 0.6f + 0.4f);
            }
            d[i] = s * env;
        }
        return Finish("Piano", Reverb(d, 0.45f), 0.8f);
    }

    // Хоровой пад: аккорд из расстроенных пил через мягкий фильтр, медленно вздыхает
    public static AudioClip Pad(int[] chord, float length)
    {
        int n = (int)(Rate * length); var d = new float[n];
        var ph = new float[chord.Length * 3];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int c = 0; c < chord.Length; c++)
                for (int k = 0; k < 3; k++)
                {
                    int idx = c * 3 + k;
                    ph[idx] = Mathf.Repeat(ph[idx] + Note(chord[c]) * (1f + (k - 1) * 0.006f) / Rate, 1f);
                    s += ph[idx] * 2f - 1f;
                }
            s /= chord.Length * 3;
            float cut = 0.03f + 0.02f * Mathf.Sin(t * 0.8f);
            lp += (s - lp) * cut; lp2 += (lp - lp2) * cut;
            float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((length - t) / 1.5f) * (0.85f + 0.15f * Mathf.Sin(t * 1.3f));
            d[i] = lp2 * env * 3f;
        }
        return Finish("Pad", Reverb(d, 0.4f), 0.7f);
    }

    // "Брам": мощный низкий медный аккорд с раскрывающимся фильтром (как в кинотрейлерах)
    public static AudioClip Braam(float length = 5f, int root = -36)
    {
        int n = (int)(Rate * length); var d = new float[n];
        int[] notes = { root, root + 7, root + 12, root + 15 };
        var ph = new float[notes.Length * 2];
        float lp = 0f, lp2 = 0f, sub = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int c = 0; c < notes.Length; c++)
                for (int k = 0; k < 2; k++)
                {
                    int idx = c * 2 + k;
                    ph[idx] = Mathf.Repeat(ph[idx] + Note(notes[c]) * (1f + (k == 0 ? -0.004f : 0.005f)) / Rate, 1f);
                    s += ph[idx] * 2f - 1f;
                }
            s /= notes.Length * 2;
            float cut = Mathf.Lerp(0.015f, 0.12f, Mathf.Exp(-t * 2.5f) * Mathf.Clamp01(t / 0.08f));
            lp += (s - lp) * cut; lp2 += (lp - lp2) * cut;
            sub += Note(root - 12) / Rate;
            float env = Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-t * 0.6f);
            d[i] = (lp2 * 3.5f + Mathf.Sin(TwoPi * sub) * 0.5f) * env;
        }
        return Finish("Braam", Reverb(d, 0.5f), 0.95f);
    }

    // Нарастание: шум с открывающимся фильтром + тон, ползущий вверх; обрывается в конце
    public static AudioClip Riser(float length)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(7);
        float lp = 0f, ph = 0f, ph2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, k = t / length;
            float w = (float)rnd.NextDouble() * 2f - 1f;
            lp += (w - lp) * Mathf.Lerp(0.01f, 0.5f, k * k);
            float f = Mathf.Lerp(110f, 880f, k * k);
            ph += f / Rate; ph2 += f * 1.5f / Rate;
            float tone = (Mathf.Sin(TwoPi * ph) + Mathf.Sin(TwoPi * ph2) * 0.5f) * 0.4f;
            float trem = 1f - 0.5f * (0.5f + 0.5f * Mathf.Sin(TwoPi * Mathf.Lerp(3f, 18f, k) * t)) * k;
            d[i] = (lp * 0.8f + tone) * k * k * trem;
        }
        return Finish("Riser", d, 0.9f);
    }

    // Свист склейки: короткий проход шума вверх-вниз
    public static AudioClip Whoosh(float length = 0.7f, int seed = 3)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(seed);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n, w = (float)rnd.NextDouble() * 2f - 1f;
            float c = 0.03f + 0.4f * Mathf.Sin(k * Mathf.PI);
            lp += (w - lp) * c; hp += (lp - hp) * 0.02f;
            d[i] = (lp - hp) * Mathf.Pow(Mathf.Sin(k * Mathf.PI), 2f);
        }
        return Finish("Whoosh", d, 0.8f);
    }

    // Удар + суб-падение для склеек и скримера
    public static AudioClip Impact(float length = 3f, float from = 90f, float to = 30f)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(11);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, w = (float)rnd.NextDouble() * 2f - 1f;
            ph += Mathf.Lerp(from, to, Mathf.Clamp01(t / 0.8f)) / Rate;
            lp += (w - lp) * Mathf.Lerp(0.6f, 0.03f, Mathf.Clamp01(t * 3f));
            d[i] = Mathf.Sin(TwoPi * ph) * Mathf.Exp(-t * 1.6f) + lp * Mathf.Exp(-t * 9f) * 0.9f;
        }
        return Finish("Impact", Reverb(d, 0.35f), 0.95f);
    }

    // Пульс: низкая нота восьмыми, как тикающий страх
    public static AudioClip Pulse(float bpm, int semi, float length = 8f)
    {
        int n = (int)(Rate * length); var d = new float[n];
        float step = 60f / bpm / 2f;
        float f = Note(semi);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, local = Mathf.Repeat(t, step);
            float accent = Mathf.Repeat(t, step * 4f) < step ? 1f : 0.6f;
            float env = Mathf.Clamp01(local / 0.005f) * Mathf.Exp(-local * 9f) * accent;
            float s = Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * f * 2f * t) + 0.2f * (Mathf.Repeat(f * t, 1f) * 2f - 1f);
            d[i] = s * env;
        }
        return Finish("Pulse", d, 0.8f);
    }

    // ---------- Атмосфера ----------

    public static AudioClip Wind(float length = 10f)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(21);
        float lp = 0f, bp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, w = (float)rnd.NextDouble() * 2f - 1f;
            float gust = 0.5f + 0.5f * Mathf.PerlinNoise(t * 0.35f, 0.3f);
            lp += (w - lp) * (0.01f + 0.04f * gust); bp += (lp - bp) * 0.005f;
            d[i] = (lp - bp) * (0.4f + gust);
        }
        return Loopable("Wind", d, 0.7f);
    }

    public static AudioClip RoomTone(float length = 8f)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(31);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, w = (float)rnd.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.02f;
            d[i] = Mathf.Sin(TwoPi * 50f * t) * 0.25f + Mathf.Sin(TwoPi * 100f * t) * 0.1f + lp * 1.2f;
        }
        return Loopable("Room Tone", d, 0.5f);
    }

    public static AudioClip Breath(float length = 6f)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(41);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, w = (float)rnd.NextDouble() * 2f - 1f;
            float cycle = Mathf.Repeat(t, 1.1f) / 1.1f;
            float env = cycle < 0.4f ? Mathf.Sin(cycle / 0.4f * Mathf.PI) : Mathf.Sin((cycle - 0.4f) / 0.6f * Mathf.PI) * 0.7f;
            float bright = cycle < 0.4f ? 0.45f : 0.2f;
            lp += (w - lp) * bright; hp += (lp - hp) * 0.05f;
            d[i] = (lp - hp) * env * env;
        }
        return Loopable("Breath", d, 0.6f);
    }

    // Скрип двери: дёрганый тон с прыгающей частотой
    public static AudioClip Creak(float length = 1.6f)
    {
        int n = (int)(Rate * length); var d = new float[n]; var rnd = new System.Random(51);
        float ph = 0f, f = 380f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, k = t / length;
            if (i % 600 == 0) f = Mathf.Lerp(280f, 520f, Mathf.PerlinNoise(t * 3f, 1.7f)) * (1f + (float)rnd.NextDouble() * 0.05f);
            ph += f / Rate;
            float stick = Mathf.Repeat(ph * 0.25f, 1f) < 0.5f ? 1f : 0.3f;
            float s = (Mathf.Repeat(ph, 1f) * 2f - 1f) * stick;
            d[i] = s * Mathf.Sin(k * Mathf.PI) * 0.5f;
        }
        return Finish("Creak", Reverb(d, 0.3f), 0.7f);
    }

    // ---------- Обработка ----------

    static float[] Reverb(float[] dry, float wet)
    {
        int n = dry.Length; var o = new float[n];
        float[] combs = { 29.7f, 37.1f, 41.1f, 43.7f };
        foreach (float ms in combs)
        {
            int dl = (int)(ms * 0.001f * Rate * 2.2f); var buf = new float[dl]; int p = 0; float damp = 0f;
            for (int i = 0; i < n; i++)
            {
                float y = buf[p]; damp = y * 0.55f + damp * 0.45f;
                buf[p] = dry[i] + damp * 0.84f; p = (p + 1) % dl;
                o[i] += y * 0.25f;
            }
        }
        for (int i = 0; i < n; i++) o[i] = dry[i] * (1f - wet * 0.5f) + o[i] * wet;
        return o;
    }

    static AudioClip Finish(string name, float[] d, float peak)
    {
        float p = 0f; foreach (float v in d) p = Mathf.Max(p, Mathf.Abs(v));
        if (p > 0f) for (int i = 0; i < d.Length; i++) d[i] *= peak / p;
        int fade = Mathf.Min(d.Length / 4, Rate / 10);
        for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= i / (float)fade;
        var c = AudioClip.Create(name, d.Length, 1, Rate, false); c.SetData(d, 0); return c;
    }

    static AudioClip Loopable(string name, float[] d, float peak)
    {
        int n = d.Length, f = Rate / 2;
        var o = new float[n - f];
        System.Array.Copy(d, 0, o, 0, n - f);
        for (int i = 0; i < f; i++) { float w = i / (float)f; o[i] = Mathf.Lerp(d[n - f + i], d[i], w); }
        float p = 0f; foreach (float v in o) p = Mathf.Max(p, Mathf.Abs(v));
        if (p > 0f) for (int i = 0; i < o.Length; i++) o[i] *= peak / p;
        var c = AudioClip.Create(name, o.Length, 1, Rate, false); c.SetData(o, 0); return c;
    }
}
