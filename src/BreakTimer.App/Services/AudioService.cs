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
using System.IO;
using System.Media;
using System.Windows.Media;
using BreakTimer.Core.Models;
using BreakTimer.Core.Services;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

/// <summary>Spielt Signaltöne ab. Ohne ausdrückliche Aktivierung wird nie etwas abgespielt (Ausnahme: "Sound testen").</summary>
public sealed class AudioService
{
    public const string CustomPreset = "Custom";
    private SoundPlayer _player;
    private MediaPlayer _media;

    /// <summary>Fehlermeldung des letzten Abspielversuchs (null = ok).</summary>
    public string LastError { get; private set; }

    public event EventHandler<string> Failed;

    /// <summary>Spielt den Ablauf-Sound, nur wenn Sound aktiviert ist.</summary>
    public bool PlayFinished(AppSettings s) => s.SoundEnabled && Play(s);

    /// <summary>Spielt den konfigurierten Sound unabhängig von der Aktivierung (für "Sound testen").</summary>
    public bool Play(AppSettings s)
    {
        LastError = null;
        try
        {
            Stop();
            if (s.SoundPreset == CustomPreset)
            {
                if (!File.Exists(s.CustomSoundPath)) return Fail("Die ausgewählte Audiodatei wurde nicht gefunden.");
                _media = new MediaPlayer { Volume = Math.Max(0, Math.Min(1, s.SoundVolume)) };
                _media.MediaFailed += (o, e) => Fail("Die Audiodatei konnte nicht abgespielt werden: " + e.ErrorException?.Message);
                _media.Open(new Uri(Path.GetFullPath(s.CustomSoundPath)));
                _media.Play();
                return true;
            }

            byte[] wav = WaveSynth.Generate(s.SoundPreset, s.SoundVolume);
            _player = new SoundPlayer(new MemoryStream(wav));
            _player.Play();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Audiowiedergabe fehlgeschlagen", ex);
            return Fail("Der Sound konnte nicht abgespielt werden: " + ex.Message);
        }
    }

    public void Stop()
    {
        try { _player?.Stop(); } catch { }
        try { _media?.Stop(); _media?.Close(); } catch { }
        _player = null;
        _media = null;
    }

    private bool Fail(string message)
    {
        LastError = message;
        Failed?.Invoke(this, message);
        return false;
    }
}
