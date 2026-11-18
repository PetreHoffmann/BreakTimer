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
using System.Windows.Threading;
using BreakTimer.Core.Models;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

public enum NoticeLevel { Info, Warning, Error }

/// <summary>
/// Orchestriert Timer, Display und Audio. Hier liegt der Ablauf von START / STOP / PAUSE ...
/// Der Stop-Vorgang hängt ausschließlich vom Control Center ab und kann nicht durch Fehler blockiert werden.
/// </summary>
public sealed class SessionController
{
    private readonly TimerService _timer;
    private readonly DisplayWindowManager _display;
    private readonly MonitorService _monitors;
    private readonly AudioService _audio;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _autoClose = new DispatcherTimer();

    public SessionController(TimerService timer, DisplayWindowManager display, MonitorService monitors, AudioService audio, AppSettings settings)
    {
        _timer = timer;
        _display = display;
        _monitors = monitors;
        _audio = audio;
        _settings = settings;
        _timer.Finished += OnFinished;
        _display.Notice += (s, msg) => Notice?.Invoke(this, new NoticeEventArgs(msg, NoticeLevel.Warning));
        _display.TestEscape += (s, e) => { if (_display.Mode == DisplayMode.Test) EndTest(); else Stop(); };
        _audio.Failed += (s, msg) => Notice?.Invoke(this, new NoticeEventArgs(msg, NoticeLevel.Warning));
        _autoClose.Tick += (s, e) => { _autoClose.Stop(); if (_timer.State == TimerState.Finished) Stop(); };
    }

    public event EventHandler<NoticeEventArgs> Notice;

    private void Say(string message, NoticeLevel level = NoticeLevel.Info) => Notice?.Invoke(this, new NoticeEventArgs(message, level));

    // ---- START -------------------------------------------------------------------------------

    public bool Start()
    {
        try
        {
            if (_timer.State == TimerState.Running) { Say("Der Timer läuft bereits."); return false; }
            if (_timer.State == TimerState.Paused) return Resume();
            if (!_timer.CanStart) { Say("Bitte eine gültige Pausendauer (1 Sekunde bis 24 Stunden) einstellen.", NoticeLevel.Warning); return false; }

            _autoClose.Stop();
            _audio.Stop();

            // Display öffnen (Fullscreen auf dem Zielmonitor, Theme wird über das gemeinsame Working-Theme übertragen).
            if (!_display.Open(DisplayMode.Running, out string error))
            {
                Say(error, NoticeLevel.Error);
                return false;
            }
            if (_display.Connection == DisplayConnection.MonitorUnavailable)
                Say("Der gewählte Zielmonitor ist nicht verfügbar. Die Anzeige läuft auf \"" + _display.CurrentMonitor?.DisplayName + "\".", NoticeLevel.Warning);

            _display.SetMode(DisplayMode.Running);
            if (!_timer.Start())
            {
                _display.Close(false);
                Say("Der Countdown konnte nicht gestartet werden.", NoticeLevel.Error);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Start fehlgeschlagen", ex);
            Say("Start fehlgeschlagen: " + ex.Message, NoticeLevel.Error);
            return false;
        }
    }

    // ---- STOP --------------------------------------------------------------------------------

    /// <summary>Beendet Countdown und Anzeige. Schlägt nie fehl und ist immer ausführbar.</summary>
    public bool Stop()
    {
        bool shared = _display.SharesWithControl;
        _autoClose.Stop();
        try { _audio.Stop(); } catch { }
        try
        {
            if (_timer.CanStop) _timer.Stop();
            else if (_timer.State == TimerState.Finished) _timer.Reset();
        }
        catch (Exception ex) { Logger.Error("Timer-Stop fehlgeschlagen", ex); }
        try { _display.Close(); } catch (Exception ex) { Logger.Error("Display-Close fehlgeschlagen", ex); }
        if (shared) BringControlCenterToFront();
        return true;
    }

    public bool Pause() => _timer.Pause();
    public bool Resume() => _timer.Resume();

    public bool TogglePause()
    {
        if (_timer.State == TimerState.Running) return _timer.Pause();
        if (_timer.State == TimerState.Paused) return _timer.Resume();
        return false;
    }

    /// <summary>Space-Taste: Start bzw. Pause/Fortsetzen.</summary>
    public bool StartOrToggle() => _timer.State == TimerState.Running || _timer.State == TimerState.Paused ? TogglePause() : Start();

    public bool Reset()
    {
        bool shared = _display.SharesWithControl;
        _autoClose.Stop();
        _audio.Stop();
        _timer.Reset();
        if (_display.Mode == DisplayMode.Running) _display.Close();
        if (shared) BringControlCenterToFront();
        return true;
    }

    public bool Adjust(TimeSpan delta)
    {
        if (!_timer.CanAdjust) { Say("Die Zeit kann nur bei bereitem, laufendem oder pausiertem Timer geändert werden.", NoticeLevel.Info); return false; }
        return _timer.Adjust(delta);
    }

    // ---- Testmodus ---------------------------------------------------------------------------

    public bool IsTestOpen => _display.IsOpen && _display.Mode == DisplayMode.Test;

    public bool StartTest()
    {
        if (_timer.IsActive || _display.Mode == DisplayMode.Running)
        {
            Say("Das Display wird bereits für die laufende Pause verwendet.");
            return false;
        }
        if (!_display.Open(DisplayMode.Test, out string error)) { Say(error, NoticeLevel.Error); return false; }
        return true;
    }

    public void EndTest()
    {
        if (_display.Mode != DisplayMode.Test) return;
        bool shared = _display.SharesWithControl;
        _display.Close();
        if (shared) BringControlCenterToFront();
    }

    // ---- Ablauf ------------------------------------------------------------------------------

    private void OnFinished(object sender, EventArgs e)
    {
        try { _audio.PlayFinished(_settings); }
        catch (Exception ex) { Logger.Error("Ablauf-Sound fehlgeschlagen", ex); }

        if (_settings.CloseDisplayAfterFinish)
        {
            _autoClose.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.CloseDisplayDelaySeconds));
            _autoClose.Start();
        }
    }

    private static void BringControlCenterToFront()
    {
        try
        {
            Window w = Application.Current?.MainWindow;
            if (w == null) return;
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
        }
        catch { }
    }
}

public sealed class NoticeEventArgs : EventArgs
{
    public NoticeEventArgs(string message, NoticeLevel level) { Message = message; Level = level; }
    public string Message { get; }
    public NoticeLevel Level { get; }
}
