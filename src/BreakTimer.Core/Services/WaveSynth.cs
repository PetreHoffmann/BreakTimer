// Copyright 2026 Petre Hoffmann
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;

namespace BreakTimer.Core.Services;

/// <summary>Erzeugt die mitgelieferten Signaltöne als WAV-Daten (16 Bit, 44,1 kHz, mono) - ohne externe Audiodateien.</summary>
public static class WaveSynth
{
    public const int SampleRate = 44100;
    public static readonly string[] Presets = { "Chime", "Bell", "Gong", "Beeps" };

    public static string Describe(string preset)
    {
        switch (preset)
        {
            case "Chime": return "Chime – sanfte Tonfolge";
            case "Bell": return "Bell – heller Glockenton";
            case "Gong": return "Gong – tiefer Gong";
            case "Beeps": return "Beeps – drei kurze Signale";
            default: return preset;
        }
    }

    /// <summary>Liefert eine vollständige WAV-Datei im Speicher. Lautstärke 0..1.</summary>
    public static byte[] Generate(string preset, double volume)
    {
        volume = Math.Max(0, Math.Min(1, volume));
        float[] samples;
        switch (preset)
        {
            case "Bell": samples = Bell(1320, 2.8); break;
            case "Gong": samples = Bell(110, 4.5); break;
            case "Beeps": samples = Beeps(); break;
            default: samples = Chime(); break;
        }
        return ToWav(samples, volume * 0.8);
    }

    private static float[] Chime()
    {
        double[] notes = { 784.0, 987.77, 1174.66, 1567.98 }; // G5 H5 D6 G6
        var buf = new float[(int)(SampleRate * 3.2)];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(n * 0.28 * SampleRate);
            AddTone(buf, start, notes[n], 1.6, 0.35, 4.0);
            AddTone(buf, start, notes[n] * 2, 1.2, 0.10, 6.0);
        }
        return buf;
    }

    private static float[] Bell(double f, double seconds)
    {
        var buf = new float[(int)(SampleRate * seconds)];
        double[] ratio = { 1.0, 2.0, 2.76, 5.4, 8.93 };
        double[] amp = { 0.5, 0.3, 0.25, 0.12, 0.06 };
        double[] decay = { 1.6, 2.2, 2.8, 4.0, 5.5 };
        for (int i = 0; i < ratio.Length; i++) AddTone(buf, 0, f * ratio[i], seconds, amp[i], decay[i]);
        return buf;
    }

    private static float[] Beeps()
    {
        var buf = new float[(int)(SampleRate * 1.6)];
        for (int i = 0; i < 3; i++) AddTone(buf, (int)(i * 0.45 * SampleRate), 1046.5, 0.3, 0.5, 6.0);
        return buf;
    }

    private static void AddTone(float[] buf, int start, double freq, double seconds, double amp, double decay)
    {
        int len = (int)(seconds * SampleRate);
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            double t = (double)i / SampleRate;
            double attack = Math.Min(1.0, t / 0.005);
            double env = attack * Math.Exp(-decay * t);
            buf[start + i] += (float)(Math.Sin(2 * Math.PI * freq * t) * amp * env);
        }
    }

    private static byte[] ToWav(float[] samples, double gain)
    {
        float peak = 0.0001f;
        foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
        double norm = gain / peak;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            int dataLen = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataLen);
            foreach (float s in samples)
            {
                double v = Math.Max(-1, Math.Min(1, s * norm));
                w.Write((short)(v * short.MaxValue));
            }
            w.Flush();
            return ms.ToArray();
        }
    }
}
