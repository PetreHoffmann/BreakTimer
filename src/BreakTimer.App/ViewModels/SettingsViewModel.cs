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
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Services;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

/// <summary>Allgemeine Einstellungen: Animationen, Audio, Verhalten nach Ablauf, Import/Export.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private const string JsonFilter = "Break-Timer-Einstellungen (*.json)|*.json";
    private readonly AppSettings _s;
    private readonly SettingsService _service;
    private readonly AudioService _audio;
    private readonly DisplayViewModel _display;
    private readonly DesignState _design;
    private readonly MonitorService _monitors;
    private readonly TimerService _timer;
    private readonly AppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly Action<string, NoticeLevel> _notify;
    private readonly Action _requestSave;
    private readonly Action _saveNow;
    private readonly Action _afterImport;

    public SettingsViewModel(AppSettings settings, SettingsService service, AudioService audio, DisplayViewModel display, DesignState design,
        MonitorService monitors, TimerService timer, AppPaths paths, IDialogService dialogs, Action<string, NoticeLevel> notify,
        Action requestSave, Action saveNow, Action afterImport)
    {
        _s = settings; _service = service; _audio = audio; _display = display; _design = design; _monitors = monitors;
        _timer = timer; _paths = paths; _dialogs = dialogs; _notify = notify; _requestSave = requestSave; _saveNow = saveNow; _afterImport = afterImport;

        TestSoundCommand = new RelayCommand(() =>
        {
            if (!_audio.Play(_s) && _audio.LastError != null) _notify(_audio.LastError, NoticeLevel.Warning);
        });
        StopSoundCommand = new RelayCommand(() => _audio.Stop());
        ChooseSoundCommand = new RelayCommand(ChooseSound);
        SaveNowCommand = new RelayCommand(() => _saveNow());
        ExportCommand = new RelayCommand(ExportSettings);
        ImportCommand = new RelayCommand(ImportSettings);
        ResetCommand = new RelayCommand(ResetDefaults);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public IReadOnlyList<string> SoundPresets { get; } = new[] { "Chime", "Bell", "Gong", "Beeps", AudioService.CustomPreset };

    public ICommand TestSoundCommand { get; }
    public ICommand StopSoundCommand { get; }
    public ICommand ChooseSoundCommand { get; }
    public ICommand SaveNowCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public string DataFolder => _paths.Root;

    public bool AnimationsEnabled
    {
        get => _s.AnimationsEnabled;
        set { _s.AnimationsEnabled = value; _display.AnimationsEnabled = value; Raise(); _requestSave(); }
    }

    public bool SoundEnabled
    {
        get => _s.SoundEnabled;
        set { _s.SoundEnabled = value; Raise(); _requestSave(); }
    }

    /// <summary>0 bis 100.</summary>
    public double SoundVolumePercent
    {
        get => Math.Round(_s.SoundVolume * 100);
        set { _s.SoundVolume = Math.Max(0, Math.Min(100, value)) / 100.0; Raise(); _requestSave(); }
    }

    public string SoundPreset
    {
        get => _s.SoundPreset;
        set { _s.SoundPreset = value ?? "Chime"; Raise(); Raise(nameof(IsCustomSound)); _requestSave(); }
    }

    public bool IsCustomSound => _s.SoundPreset == AudioService.CustomPreset;

    public string CustomSoundPath
    {
        get => _s.CustomSoundPath;
        set { _s.CustomSoundPath = value ?? ""; Raise(); Raise(nameof(CustomSoundDisplay)); _requestSave(); }
    }

    public string CustomSoundDisplay => string.IsNullOrEmpty(_s.CustomSoundPath) ? "Keine Datei gewählt" : Path.GetFileName(_s.CustomSoundPath);

    /// <summary>Wird aufgerufen, wenn sich "Bildschirm wach halten" ändert.</summary>
    public Action KeepAwakeChanged { get; set; }

    public bool KeepAwakeDuringBreak
    {
        get => _s.KeepAwakeDuringBreak;
        set { _s.KeepAwakeDuringBreak = value; Raise(); _requestSave(); KeepAwakeChanged?.Invoke(); }
    }

    public bool CloseDisplayAfterFinish
    {
        get => _s.CloseDisplayAfterFinish;
        set { _s.CloseDisplayAfterFinish = value; Raise(); _requestSave(); }
    }

    public string CloseDelayText
    {
        get => _s.CloseDisplayDelaySeconds.ToString();
        set
        {
            if (int.TryParse(value, out int v) && v >= 1 && v <= 3600) { _s.CloseDisplayDelaySeconds = v; _requestSave(); }
            Raise();
        }
    }

    private void ChooseSound()
    {
        string file = _dialogs.OpenFile("Audiodateien (WAV, MP3, WMA)|*.wav;*.mp3;*.wma|Alle Dateien|*.*", "Sounddatei wählen");
        if (file == null) return;
        CustomSoundPath = file;
        SoundPreset = AudioService.CustomPreset;
    }

    private void ExportSettings()
    {
        try
        {
            string path = _dialogs.SaveFile(JsonFilter, "BreakTimer-Einstellungen.json", "Einstellungen exportieren");
            if (path == null) return;
            _service.Export(_s, path);
            _notify("Einstellungen exportiert.", NoticeLevel.Info);
        }
        catch (Exception ex) { _notify("Export fehlgeschlagen: " + ex.Message, NoticeLevel.Error); }
    }

    private void ImportSettings()
    {
        try
        {
            string path = _dialogs.OpenFile(JsonFilter + "|Alle Dateien|*.*", "Einstellungen importieren");
            if (path == null) return;
            AppSettings imported = _service.Import(path);
            if (!_dialogs.Confirm("Die aktuellen Einstellungen werden durch die importierten ersetzt. Fortfahren?")) return;
            ApplyLive(imported);
            _notify("Einstellungen importiert.", NoticeLevel.Info);
        }
        catch (Exception ex)
        {
            Logger.Error("Import fehlgeschlagen", ex);
            _notify("Import fehlgeschlagen: " + ex.Message, NoticeLevel.Error);
        }
    }

    private void ResetDefaults()
    {
        try
        {
            if (!_dialogs.Confirm("Alle Einstellungen auf Standardwerte zurücksetzen? Eigene Themes bleiben erhalten.")) return;
            AppSettings defaults = SettingsService.CreateDefaults();
            ApplyLive(defaults);
            _notify("Standardwerte wiederhergestellt.", NoticeLevel.Info);
        }
        catch (Exception ex) { _notify("Zurücksetzen fehlgeschlagen: " + ex.Message, NoticeLevel.Error); }
    }

    private void ApplyLive(AppSettings source)
    {
        string displayId = source.DisplayMonitorId, controlId = source.ControlMonitorId;
        _s.CopyFrom(source);
        _display.AnimationsEnabled = _s.AnimationsEnabled;
        if (!_timer.IsActive) _timer.SetDuration(TimeSpan.FromSeconds(_s.DurationSeconds));
        _monitors.SetPreferred(displayId, controlId);
        // Working-Theme ist bereits aktualisiert (CopyFrom); die Quelle neu zu bestimmen ist Aufgabe von afterImport.
        _afterImport();
        foreach (string n in new[] { nameof(AnimationsEnabled), nameof(SoundEnabled), nameof(SoundVolumePercent), nameof(SoundPreset),
            nameof(IsCustomSound), nameof(CustomSoundPath), nameof(CustomSoundDisplay), nameof(CloseDisplayAfterFinish), nameof(CloseDelayText), nameof(KeepAwakeDuringBreak) })
            Raise(n);
        _saveNow();
    }

    private void OpenFolder()
    {
        try
        {
            _paths.EnsureDirectories();
            Process.Start("explorer.exe", "\"" + _paths.Root + "\"");
        }
        catch (Exception ex) { _notify("Ordner konnte nicht geöffnet werden: " + ex.Message, NoticeLevel.Warning); }
    }
}
