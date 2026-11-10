using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Синтезирует все звуки игры в WAV-файлы (без скачанных ассетов) и импортирует их как AudioClip.
public static class ARGameAudio
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    public class Clips
    {
        public AudioClip place, remove, click, shoot, start, reset, win, ballHit;
        public AudioClip[] woodHits;
    }

    static string folder;

    public static Clips CreateAll(string root)
    {
        folder = root + "/Audio";
        ARGameSetup.EnsureFolder(folder);

        var c = new Clips
        {
            place = Save("place", Pop()),
            remove = Save("remove", Remove()),
            click = Save("click", Click()),
            shoot = Save("shoot", Whoosh()),
            start = Save("start", Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.085f, 0.3f)),
            win = Save("win", Fanfare()),
            reset = Save("reset", Sweep(900f, 280f, 0.28f)),
            ballHit = Save("ball_hit", Thud()),
            woodHits = new[]
            {
                Save("wood_1", Knock(520f, 1)),
                Save("wood_2", Knock(610f, 2)),
                Save("wood_3", Knock(700f, 3)),
                Save("wood_4", Knock(450f, 4)),
            },
        };
        return c;
    }

    // ---------- звуки ----------

    // «Поп» при установке фигуры: короткий восходящий тон
    static float[] Pop()
    {
        return Synth(0.12f, (t, s) =>
        {
            float f = Mathf.Lerp(420f, 950f, Mathf.Clamp01(t / 0.06f));
            s.phase += TwoPi * f / Rate;
            float env = Mathf.Exp(-t * 38f) * (1f - Mathf.Exp(-t * 3000f));
            return Mathf.Sin(s.phase) * env;
        });
    }

    // Отмена / очистка: два коротких нисходящих «блипа»
    static float[] Remove()
    {
        return Synth(0.22f, (t, s) =>
        {
            float local = t < 0.1f ? t : t - 0.1f;
            float f = (t < 0.1f ? 720f : 520f) - local * 900f;
            s.phase += TwoPi * f / Rate;
            float env = Mathf.Exp(-local * 30f) * (1f - Mathf.Exp(-local * 3000f));
            return Mathf.Sin(s.phase) * env * 0.8f;
        });
    }

    // Клик кнопки
    static float[] Click()
    {
        return Synth(0.04f, (t, s) =>
        {
            s.phase += TwoPi * 1900f / Rate;
            return (Mathf.Sin(s.phase) * 0.7f + s.Noise() * 0.3f) * Mathf.Exp(-t * 160f);
        });
    }

    // Выстрел: глухой «бум» + шум, у которого фильтр закрывается (вжух)
    static float[] Whoosh()
    {
        return Synth(0.4f, (t, s) =>
        {
            float cutoff = Mathf.Lerp(4200f, 500f, Mathf.Clamp01(t / 0.35f));
            s.lp += (s.Noise() - s.lp) * (1f - Mathf.Exp(-TwoPi * cutoff / Rate));
            float noiseEnv = (1f - Mathf.Exp(-t * 90f)) * Mathf.Exp(-t * 9f);
            float bf = Mathf.Lerp(160f, 60f, Mathf.Clamp01(t / 0.12f));
            s.phase += TwoPi * bf / Rate;
            float boom = Mathf.Sin(s.phase) * Mathf.Exp(-t * 28f);
            return s.lp * noiseEnv * 1.6f + boom * 0.8f;
        });
    }

    // Деревянный стук: несколько негармонических частот + щелчок в начале
    static float[] Knock(float baseF, int seed)
    {
        float[] ratios = { 1f, 2.76f, 5.4f, 8.9f };
        float[] amps = { 1f, 0.55f, 0.28f, 0.12f };
        float[] decays = { 38f, 55f, 85f, 120f };
        var phases = new float[ratios.Length];
        return Synth(0.22f, (t, s) =>
        {
            float sum = 0f;
            for (int i = 0; i < ratios.Length; i++)
            {
                phases[i] += TwoPi * baseF * ratios[i] / Rate;
                sum += Mathf.Sin(phases[i]) * amps[i] * Mathf.Exp(-t * decays[i]);
            }
            float click = s.Noise() * Mathf.Exp(-t * 900f) * 0.8f;
            return (sum * 0.6f + click) * (1f - Mathf.Exp(-t * 4000f));
        }, seed);
    }

    // Удар шара: низкий «тум» с падающей высотой
    static float[] Thud()
    {
        return Synth(0.3f, (t, s) =>
        {
            float f = Mathf.Lerp(150f, 70f, Mathf.Clamp01(t / 0.15f));
            s.phase += TwoPi * f / Rate;
            s.lp += (s.Noise() - s.lp) * 0.15f;
            float body = Mathf.Sin(s.phase) * Mathf.Exp(-t * 18f);
            float hit = s.lp * Mathf.Exp(-t * 60f) * 1.5f;
            return (body + hit) * (1f - Mathf.Exp(-t * 3000f));
        });
    }

    // Сброс: «вжух» вниз
    static float[] Sweep(float from, float to, float dur)
    {
        return Synth(dur, (t, s) =>
        {
            float k = t / dur;
            float f = Mathf.Lerp(from, to, k * k);
            s.phase += TwoPi * f / Rate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * 0.9f;
            return (Mathf.Sin(s.phase) * 0.7f + Mathf.Sin(s.phase * 2f) * 0.15f) * env;
        });
    }

    // Арпеджио (старт): ноты подряд, последняя тянется
    static float[] Arpeggio(float[] notes, float step, float tail)
    {
        float dur = step * (notes.Length - 1) + tail;
        var phases = new float[notes.Length];
        return Synth(dur, (t, s) =>
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * step;
                if (local < 0f) continue;
                phases[i] += TwoPi * notes[i] / Rate;
                float decay = i == notes.Length - 1 ? 7f : 14f;
                float env = Mathf.Exp(-local * decay) * (1f - Mathf.Exp(-local * 600f));
                sum += (Mathf.Sin(phases[i]) + 0.25f * Mathf.Sin(phases[i] * 3f)) * env;
            }
            return sum * 0.5f;
        });
    }

    // Победные фанфары: арпеджио + финальный аккорд
    static float[] Fanfare()
    {
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
        const float step = 0.11f;
        float chordStart = step * notes.Length;
        var p1 = new float[notes.Length];
        var p2 = new float[chord.Length];
        return Synth(chordStart + 1.0f, (t, s) =>
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * step;
                if (local < 0f || local > 0.3f) continue;
                p1[i] += TwoPi * notes[i] / Rate;
                sum += (Mathf.Sin(p1[i]) + 0.3f * Mathf.Sin(p1[i] * 2f)) * Mathf.Exp(-local * 12f) * (1f - Mathf.Exp(-local * 500f));
            }
            float lc = t - chordStart;
            if (lc >= 0f)
            {
                float vib = 1f + 0.004f * Mathf.Sin(TwoPi * 5.5f * lc);
                for (int i = 0; i < chord.Length; i++)
                {
                    p2[i] += TwoPi * chord[i] * vib / Rate;
                    sum += (Mathf.Sin(p2[i]) + 0.2f * Mathf.Sin(p2[i] * 2f)) * 0.55f * Mathf.Exp(-lc * 2.8f) * (1f - Mathf.Exp(-lc * 300f));
                }
            }
            return sum * 0.45f;
        });
    }

    // ---------- движок ----------

    class State
    {
        public float phase, lp;
        readonly System.Random rng;
        public State(int seed) { rng = new System.Random(seed); }
        public float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    }

    static float[] Synth(float dur, Func<float, State, float> f, int seed = 0)
    {
        int n = Mathf.CeilToInt(dur * Rate);
        var data = new float[n];
        var s = new State(seed + 12345);
        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            data[i] = f(t, s);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        // нормализация + короткое затухание в конце, чтобы не было щелчка
        float gain = peak > 0f ? 0.9f / peak : 1f;
        int fade = Mathf.Min(n, Rate / 200);
        for (int i = 0; i < n; i++)
        {
            float g = gain;
            int fromEnd = n - 1 - i;
            if (fromEnd < fade) g *= (float)fromEnd / fade;
            data[i] *= g;
        }
        return data;
    }

    static AudioClip Save(string name, float[] data)
    {
        string path = $"{folder}/{name}.wav";
        WriteWav(path, data);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void WriteWav(string path, float[] samples)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // моно
            w.Write(Rate);
            w.Write(Rate * 2);      // байт в секунду
            w.Write((short)2);      // выравнивание блока
            w.Write((short)16);     // бит на сэмпл
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(dataBytes);
            foreach (float s in samples)
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
