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
using System.Windows;
using BreakTimer.App.ViewModels;
using BreakTimer.App.Views;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

public enum DisplayMode { None, Running, Test }
public enum DisplayConnection { Closed, Connected, MonitorUnavailable }

/// <summary>
/// Öffnet, positioniert und schließt das Break Display (genau ein Fenster). Reagiert auf Monitoränderungen
/// und stellt sicher, dass das Control Center unabhängig vom Display bedienbar bleibt.
/// </summary>
public sealed class DisplayWindowManager
{
    private readonly MonitorService _monitors;
    private readonly DisplayViewModel _displayVm;
    private DisplayWindow _window;
    private string _lastMonitorId;

    public DisplayWindowManager(MonitorService monitors, DisplayViewModel displayVm)
    {
        _monitors = monitors;
        _displayVm = displayVm;
        monitors.MonitorsChanged += (s, e) => Reposition(true);
        monitors.SelectionChanged += (s, e) => Reposition(false);
    }

    public DisplayMode Mode { get; private set; } = DisplayMode.None;
    public DisplayConnection Connection { get; private set; } = DisplayConnection.Closed;
    public bool IsOpen => _window != null;
    public MonitorInfo CurrentMonitor { get; private set; }
    public bool SharesWithControl { get; private set; }

    public event EventHandler StateChanged;
    /// <summary>Hinweis für den Benutzer (Text, ist Warnung).</summary>
    public event EventHandler<string> Notice;
    /// <summary>Im Testmodus wurde Escape auf dem Display gedrückt.</summary>
    public event EventHandler TestEscape;

    /// <summary>Öffnet das Display auf dem Zielmonitor. Liefert false und eine Fehlermeldung, wenn das nicht möglich ist.</summary>
    public bool Open(DisplayMode mode, out string error)
    {
        error = null;
        try
        {
            if (_window != null)
            {
                SetMode(mode);
                Reposition(false);
                return true;
            }

            _monitors.Refresh();
            MonitorResolution res = _monitors.ResolveDisplay();
            if (res.Monitor == null)
            {
                error = "Es wurde kein Monitor gefunden. Bitte Monitore im Bereich \"Monitore\" neu erkennen.";
                return false;
            }

            var w = new DisplayWindow(_displayVm) { EscapeEnabled = true };
            w.EscapePressed += (s, e) => TestEscape?.Invoke(this, EventArgs.Empty);
            w.Closed += OnWindowClosed;
            _window = w;
            Mode = mode;
            ApplyResolution(res, announce: false);
            w.SetTarget(res.Monitor.Bounds, topmost: true);
            w.Show();
            // Ein-Monitor-Betrieb: Display nimmt den Tastaturfokus, damit Esc sofort wirkt.
            if (res.SharesWithControl) { try { w.Activate(); w.Focus(); } catch { } }
            RaiseState();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Break Display konnte nicht geöffnet werden", ex);
            error = "Das Break Display konnte nicht geöffnet werden: " + ex.Message;
            DisposeWindow(false);
            return false;
        }
    }

    public void SetMode(DisplayMode mode)
    {
        if (_window == null) return;
        Mode = mode;
        _window.EscapeEnabled = true;
        RaiseState();
    }

    public void Close(bool animate = true)
    {
        DisplayWindow w = _window;
        DisposeWindow(animate);
        if (w != null) RaiseState();
    }

    private void DisposeWindow(bool animate)
    {
        DisplayWindow w = _window;
        _window = null;
        Mode = DisplayMode.None;
        Connection = DisplayConnection.Closed;
        CurrentMonitor = null;
        SharesWithControl = false;
        _lastMonitorId = null;
        if (w == null) return;
        w.Closed -= OnWindowClosed;
        try { if (animate) w.CloseAnimated(); else w.Close(); }
        catch (Exception ex) { Logger.Error("Display konnte nicht geschlossen werden", ex); }
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        // Fenster wurde extern geschlossen (z. B. Alt+F4) - Zustand angleichen.
        if (_window == sender)
        {
            _window = null;
            Mode = DisplayMode.None;
            Connection = DisplayConnection.Closed;
            CurrentMonitor = null;
            SharesWithControl = false;
            RaiseState();
        }
    }

    private void ApplyResolution(MonitorResolution res, bool announce)
    {
        bool movedMonitor = _lastMonitorId != null && !string.Equals(_lastMonitorId, res.Monitor.Id, StringComparison.OrdinalIgnoreCase);
        CurrentMonitor = res.Monitor;
        SharesWithControl = res.SharesWithControl;
        Connection = res.IsFallback ? DisplayConnection.MonitorUnavailable : DisplayConnection.Connected;
        _lastMonitorId = res.Monitor.Id;
        if (announce && movedMonitor)
        {
            Notice?.Invoke(this, res.IsFallback
                ? $"Der gewählte Monitor ist nicht mehr verfügbar. Die Anzeige wurde auf \"{res.Monitor.DisplayName}\" verschoben."
                : $"Die Anzeige wurde auf \"{res.Monitor.DisplayName}\" verschoben.");
        }
    }

    /// <summary>Positioniert das Display neu (nach Änderung der Monitore oder der Zuordnung).</summary>
    public void Reposition(bool monitorsChanged)
    {
        if (_window == null) return;
        try
        {
            MonitorResolution res = _monitors.ResolveDisplay();
            if (res.Monitor == null)
            {
                Connection = DisplayConnection.MonitorUnavailable;
                Notice?.Invoke(this, "Es ist kein Monitor mehr verfügbar.");
                RaiseState();
                return;
            }
            ApplyResolution(res, announce: true);
            _window.SetTarget(res.Monitor.Bounds, topmost: true);
            RaiseState();
        }
        catch (Exception ex)
        {
            Logger.Error("Display konnte nicht neu positioniert werden", ex);
        }
    }

    private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);
}
