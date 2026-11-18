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
using System.Text;
using BreakTimer.Core.Models;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Services;

/// <summary>Lädt und speichert die Einstellungen (JSON in %AppData%\BreakTimer). Robust gegen beschädigte Dateien.</summary>
public sealed class SettingsService
{
    private readonly AppPaths _paths;
    private readonly object _gate = new object();

    public SettingsService(AppPaths paths) { _paths = paths ?? throw new ArgumentNullException(nameof(paths)); }

    /// <summary>Hinweis zum letzten Ladevorgang (z. B. beschädigte Datei), sonst null.</summary>
    public string LastLoadWarning { get; private set; }

    public static AppSettings CreateDefaults()
    {
        var s = new AppSettings { CurrentDesign = BuiltInThemes.NeonFuture() };
        return s;
    }

    public AppSettings Load()
    {
        LastLoadWarning = null;
        try
        {
            _paths.EnsureDirectories();
            if (!File.Exists(_paths.SettingsFile))
            {
                return CreateDefaults();
            }
            try
            {
                return Normalize(JsonService.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsFile, Encoding.UTF8)));
            }
            catch (Exception ex)
            {
                Logger.Error("settings.json beschädigt", ex);
                QuarantineCorruptFile();
                if (File.Exists(_paths.SettingsBackupFile))
                {
                    try
                    {
                        var restored = Normalize(JsonService.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsBackupFile, Encoding.UTF8)));
                        LastLoadWarning = "Die Einstellungsdatei war beschädigt und wurde aus der Sicherung wiederhergestellt.";
                        return restored;
                    }
                    catch (Exception ex2) { Logger.Error("Sicherung ebenfalls unlesbar", ex2); }
                }
                LastLoadWarning = "Die Einstellungsdatei war beschädigt. Es wurden Standardwerte geladen.";
                return CreateDefaults();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Einstellungen konnten nicht geladen werden", ex);
            LastLoadWarning = "Einstellungen konnten nicht geladen werden (" + ex.Message + "). Es wurden Standardwerte geladen.";
            return CreateDefaults();
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            string target = _paths.SettingsFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(_paths.SettingsFile, target);
        }
        catch { /* ignorieren */ }
    }

    /// <summary>Speichert atomar (temporäre Datei + Ersetzen) und legt eine Sicherung an.</summary>
    public bool TrySave(AppSettings settings, out string error)
    {
        error = null;
        try
        {
            lock (_gate)
            {
                _paths.EnsureDirectories();
                string json = JsonService.Serialize(settings);
                string tmp = _paths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(_paths.SettingsFile))
                    File.Replace(tmp, _paths.SettingsFile, _paths.SettingsBackupFile, true);
                else
                    File.Move(tmp, _paths.SettingsFile);
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Einstellungen konnten nicht gespeichert werden", ex);
            error = ex.Message;
            return false;
        }
    }

    public void Export(AppSettings settings, string path)
    {
        File.WriteAllText(path, JsonService.Serialize(settings), new UTF8Encoding(false));
    }

    /// <summary>Liest eine exportierte Einstellungsdatei. Wirft InvalidDataException bei ungültigem Inhalt.</summary>
    public AppSettings Import(string path)
    {
        try
        {
            return Normalize(JsonService.Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8)));
        }
        catch (IOException) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("Die Datei enthält keine gültigen Einstellungen: " + ex.Message, ex); }
    }

    public static AppSettings Normalize(AppSettings s)
    {
        if (s.CurrentDesign == null) s.CurrentDesign = BuiltInThemes.NeonFuture();
        ThemeValidator.Sanitize(s.CurrentDesign);
        if (!Timer.TimerService.ValidDuration(TimeSpan.FromSeconds(s.DurationSeconds))) s.DurationSeconds = 15 * 60;
        if (double.IsNaN(s.SoundVolume) || s.SoundVolume < 0) s.SoundVolume = 0; else if (s.SoundVolume > 1) s.SoundVolume = 1;
        if (s.CloseDisplayDelaySeconds < 0) s.CloseDisplayDelaySeconds = 0;
        if (string.IsNullOrEmpty(s.SoundPreset)) s.SoundPreset = "Chime";
        s.DisplayMonitorId = s.DisplayMonitorId ?? "";
        s.ControlMonitorId = s.ControlMonitorId ?? "";
        s.CustomSoundPath = s.CustomSoundPath ?? "";
        s.ActiveThemeId = s.ActiveThemeId ?? BuiltInThemes.NeonFutureId;
        return s;
    }
}
