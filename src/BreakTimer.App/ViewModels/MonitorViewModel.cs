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
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Monitoring;

namespace BreakTimer.App.ViewModels;

public sealed class MonitorItemViewModel : ViewModelBase
{
    private bool _isDisplay;
    private bool _isControl;

    public MonitorItemViewModel(MonitorInfo info) { Info = info; }

    public MonitorInfo Info { get; }
    public string Title => Info.DisplayName;
    public override string ToString() => Info.DisplayName;
    public string Number => Info.Number.ToString();
    public string Details => $"{Info.Resolution}  •  Skalierung {Info.DpiText}  •  Position {Info.Bounds.X}, {Info.Bounds.Y}";
    public string WorkArea => $"Arbeitsbereich {Info.WorkingArea.Width} × {Info.WorkingArea.Height}";
    public string Kind => Info.IsPrimary ? "Hauptmonitor" : "Erweiterter Bildschirm";
    public string Name => string.IsNullOrWhiteSpace(Info.FriendlyName) ? "Unbekannter Monitor" : Info.FriendlyName;

    public bool IsDisplay { get => _isDisplay; set => Set(ref _isDisplay, value); }
    public bool IsControl { get => _isControl; set => Set(ref _isControl, value); }

    // Skalierte Karte (Vorschau der Monitoranordnung)
    public double MapLeft { get; set; }
    public double MapTop { get; set; }
    public double MapWidth { get; set; }
    public double MapHeight { get; set; }
}

/// <summary>Monitorübersicht, Auswahl von Anzeige- und Steuerungsmonitor.</summary>
public sealed class MonitorViewModel : ViewModelBase
{
    private const double MapW = 640, MapH = 250;
    private readonly MonitorService _monitors;
    private readonly AppSettings _settings;
    private readonly Action _requestSave;
    private MonitorItemViewModel _selectedDisplay;
    private bool _updating;

    public MonitorViewModel(MonitorService monitors, AppSettings settings, Action requestSave)
    {
        _monitors = monitors;
        _settings = settings;
        _requestSave = requestSave ?? (() => { });
        monitors.MonitorsChanged += (s, e) => Rebuild();
        monitors.SelectionChanged += (s, e) => UpdateFlags();
        RefreshCommand = new RelayCommand(() => { _monitors.RefreshAndNotify(); Rebuild(); });
        SetDisplayCommand = new RelayCommand(p => { if (p is MonitorItemViewModel m) SetDisplay(m); });
        SetControlCommand = new RelayCommand(p => { if (p is MonitorItemViewModel m) SetControl(m); });
        IdentifyCommand = new RelayCommand(() => MonitorIdentifier.Show(_monitors.Monitors));
        Rebuild();
    }

    public ObservableCollection<MonitorItemViewModel> Items { get; } = new ObservableCollection<MonitorItemViewModel>();
    public ICommand RefreshCommand { get; }
    public ICommand SetDisplayCommand { get; }
    public ICommand SetControlCommand { get; }
    public ICommand IdentifyCommand { get; }

    public string Summary => Items.Count == 1 ? "1 Monitor erkannt" : Items.Count + " Monitore erkannt";

    public string DisplayTargetText => _monitors.ResolveDisplay().Monitor?.DisplayName ?? "Kein Monitor";
    public string ControlMonitorText => _monitors.ResolveControl()?.DisplayName ?? "Kein Monitor";

    public string WarningText
    {
        get
        {
            if (Items.Count == 0) return "Es wurde kein Monitor erkannt.";
            if (Items.Count == 1) return "Nur ein Monitor erkannt: Das Break Display erscheint auf demselben Bildschirm. Eine kleine Mini-Steuerung bleibt dann sichtbar, um die Pause zu beenden.";
            MonitorResolution r = _monitors.ResolveDisplay();
            if (r.IsFallback) return "Der zuvor gewählte Anzeigemonitor ist nicht verfügbar – es wird ein Ersatzmonitor verwendet.";
            if (r.SharesWithControl) return "Anzeige- und Steuerungsmonitor sind identisch. Für Schulungen empfiehlt sich ein getrennter Anzeigemonitor.";
            return "";
        }
    }

    public bool HasWarning => !string.IsNullOrEmpty(WarningText);

    /// <summary>Für ComboBoxen auf dem Dashboard.</summary>
    public MonitorItemViewModel SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (_updating || value == null || ReferenceEquals(value, _selectedDisplay)) return;
            SetDisplay(value);
        }
    }

    private void SetDisplay(MonitorItemViewModel m)
    {
        _monitors.SetPreferredDisplay(m.Info.Id);
        _settings.DisplayMonitorId = m.Info.Id;
        _requestSave();
    }

    private void SetControl(MonitorItemViewModel m)
    {
        _monitors.SetPreferredControl(m.Info.Id);
        _settings.ControlMonitorId = m.Info.Id;
        // Ist der Anzeigemonitor automatisch gewählt, wandert er mit.
        _requestSave();
    }

    private void Rebuild()
    {
        _updating = true;
        try
        {
            Items.Clear();
            var list = _monitors.Monitors;
            if (list.Count > 0)
            {
                int minX = list.Min(m => m.Bounds.X), minY = list.Min(m => m.Bounds.Y);
                int maxX = list.Max(m => m.Bounds.Right), maxY = list.Max(m => m.Bounds.Bottom);
                double scale = Math.Min((MapW - 20) / Math.Max(1, maxX - minX), (MapH - 20) / Math.Max(1, maxY - minY));
                double offX = (MapW - (maxX - minX) * scale) / 2, offY = (MapH - (maxY - minY) * scale) / 2;
                foreach (MonitorInfo m in list)
                {
                    Items.Add(new MonitorItemViewModel(m)
                    {
                        MapLeft = offX + (m.Bounds.X - minX) * scale,
                        MapTop = offY + (m.Bounds.Y - minY) * scale,
                        MapWidth = m.Bounds.Width * scale - 4,
                        MapHeight = m.Bounds.Height * scale - 4
                    });
                }
            }
        }
        finally { _updating = false; }
        UpdateFlags();
        Raise(nameof(Summary));
    }

    private void UpdateFlags()
    {
        _updating = true;
        try
        {
            MonitorInfo disp = _monitors.ResolveDisplay().Monitor;
            MonitorInfo ctrl = _monitors.ResolveControl();
            foreach (MonitorItemViewModel i in Items)
            {
                i.IsDisplay = disp != null && i.Info.Id == disp.Id;
                i.IsControl = ctrl != null && i.Info.Id == ctrl.Id;
            }
            _selectedDisplay = Items.FirstOrDefault(i => i.IsDisplay);
        }
        finally { _updating = false; }
        Raise(nameof(SelectedDisplay));
        Raise(nameof(DisplayTargetText));
        Raise(nameof(ControlMonitorText));
        Raise(nameof(WarningText));
        Raise(nameof(HasWarning));
    }
}
