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

namespace BreakTimer.Core.Models;

/// <summary>Persistente Anwendungseinstellungen (settings.json).</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    // Monitore
    public string DisplayMonitorId { get; set; } = "";
    public string ControlMonitorId { get; set; } = "";

    // Timer
    public int DurationSeconds { get; set; } = 15 * 60;
    public bool CloseDisplayAfterFinish { get; set; }
    public int CloseDisplayDelaySeconds { get; set; } = 30;

    // Design
    public string ActiveThemeId { get; set; } = BuiltInThemes.NeonFutureId;
    /// <summary>Aktuelles (ggf. bearbeitetes) Design inkl. ungespeicherter Änderungen.</summary>
    public Theme CurrentDesign { get; set; }
    public bool AnimationsEnabled { get; set; } = true;

    // Audio
    public bool SoundEnabled { get; set; }
    public double SoundVolume { get; set; } = 0.7;
    public string SoundPreset { get; set; } = "Chime";
    public string CustomSoundPath { get; set; } = "";

    /// <summary>Übernimmt alle Werte in diese Instanz (Referenzen anderer Komponenten bleiben gültig).
    /// Das CurrentDesign-Objekt bleibt dieselbe Instanz, nur sein Inhalt wird ersetzt.</summary>
    public void CopyFrom(AppSettings o)
    {
        Version = o.Version;
        DisplayMonitorId = o.DisplayMonitorId; ControlMonitorId = o.ControlMonitorId;
        DurationSeconds = o.DurationSeconds; CloseDisplayAfterFinish = o.CloseDisplayAfterFinish; CloseDisplayDelaySeconds = o.CloseDisplayDelaySeconds;
        ActiveThemeId = o.ActiveThemeId; AnimationsEnabled = o.AnimationsEnabled;
        SoundEnabled = o.SoundEnabled; SoundVolume = o.SoundVolume; SoundPreset = o.SoundPreset; CustomSoundPath = o.CustomSoundPath;
        if (o.CurrentDesign != null)
        {
            if (CurrentDesign == null) CurrentDesign = o.CurrentDesign.Clone();
            else CurrentDesign.CopyFrom(o.CurrentDesign);
        }
    }
}
