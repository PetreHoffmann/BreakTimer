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
using System.Globalization;
using System.Windows.Input;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

/// <summary>Darstellung und Steuerung des (einzigen) TimerService für Control Center und Mini-Steuerung.</summary>
public sealed class TimerViewModel : ViewModelBase
{
    private readonly TimerService _timer;
    private readonly SessionController _session;
    private readonly DisplayWindowManager _display;
    private readonly AppSettings _settings;
    private readonly Action _requestSave;
    private string _minutesInput;
    private string _secondsInput;
    private string _customMinutes = "2";
    private string _validationMessage = "";
    private bool _syncing;

    public TimerViewModel(TimerService timer, SessionController session, DisplayWindowManager display, AppSettings settings, Action requestSave)
    {
        _timer = timer;
        _session = session;
        _display = display;
        _settings = settings;
        _requestSave = requestSave ?? (() => { });
        SyncInputs();

        _timer.Tick += (s, e) => Refresh();
        _timer.StateChanged += (s, e) => Refresh();
        _display.StateChanged += (s, e) => Refresh();

        StartCommand = new RelayCommand(Start, () => _timer.CanStart || _timer.State == TimerState.Paused);
        PauseCommand = new RelayCommand(() => _session.Pause(), () => _timer.CanPause);
        ResumeCommand = new RelayCommand(() => _session.Resume(), () => _timer.CanResume);
        StopCommand = new RelayCommand(() => _session.Stop(), () => _timer.IsActive || _display.IsOpen);
        ResetCommand = new RelayCommand(() => { _session.Reset(); SyncInputs(); });
        AdjustCommand = new RelayCommand(p => Adjust(ParseMinutes(p)), _ => _timer.CanAdjust);
        AddCustomCommand = new RelayCommand(() => AdjustCustom(+1), () => _timer.CanAdjust);
        SubCustomCommand = new RelayCommand(() => AdjustCustom(-1), () => _timer.CanAdjust);
        PresetCommand = new RelayCommand(p => SetPreset(ParseMinutes(p)), _ => !_timer.IsActive);
        TestDisplayCommand = new RelayCommand(() => { if (_session.IsTestOpen) _session.EndTest(); else _session.StartTest(); Refresh(); });
        EndTestCommand = new RelayCommand(() => _session.EndTest(), () => _session.IsTestOpen);
    }

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand AdjustCommand { get; }
    public ICommand AddCustomCommand { get; }
    public ICommand SubCustomCommand { get; }
    public ICommand PresetCommand { get; }
    public ICommand TestDisplayCommand { get; }
    public ICommand EndTestCommand { get; }

    public TimerService Timer => _timer;
    public SessionController Session => _session;

    // ---- Anzeige -----------------------------------------------------------------------------

    public TimerState State => _timer.State;
    public string RemainingText => TimeFormatter.Format(_timer.Remaining);
    public string DurationText => TimeFormatter.Format(_timer.Duration);
    public string EndTimeText => _timer.State == TimerState.Finished ? "–" : TimeFormatter.FormatClock(_timer.ExpectedEnd) + " Uhr";
    public bool IsRunning => _timer.State == TimerState.Running;
    public bool IsPaused => _timer.State == TimerState.Paused;
    public bool IsActive => _timer.IsActive;
    public bool IsFinished => _timer.State == TimerState.Finished;
    public bool CanEditDuration => !_timer.IsActive;
    public bool IsTestOpen => _session.IsTestOpen;

    public double Progress
    {
        get
        {
            double d = _timer.Duration.TotalSeconds;
            if (d <= 0) return 0;
            return Math.Max(0, Math.Min(1, 1 - _timer.Remaining.TotalSeconds / d));
        }
    }

    public string StatusText
    {
        get
        {
            switch (_timer.State)
            {
                case TimerState.Running: return "Pause läuft";
                case TimerState.Paused: return "Pause pausiert";
                case TimerState.Finished: return "Pause beendet";
                case TimerState.Stopped: return "Gestoppt";
                default: return _session.IsTestOpen ? "Testanzeige aktiv" : "Bereit";
            }
        }
    }

    /// <summary>Ok, Active, Warn, Error - steuert die Farbe des Statuspunkts.</summary>
    public string StatusLevel
    {
        get
        {
            switch (_timer.State)
            {
                case TimerState.Running: return "Active";
                case TimerState.Paused: return "Warn";
                case TimerState.Finished: return "Ok";
                default: return "Idle";
            }
        }
    }

    public string DisplayStatusText
    {
        get
        {
            switch (_display.Connection)
            {
                case DisplayConnection.Connected: return "Display verbunden – " + _display.CurrentMonitor?.DisplayName;
                case DisplayConnection.MonitorUnavailable: return "Monitor nicht verfügbar – Ausweichmonitor: " + _display.CurrentMonitor?.DisplayName;
                default: return "Display nicht verbunden";
            }
        }
    }

    public string DisplayStatusLevel
    {
        get
        {
            switch (_display.Connection)
            {
                case DisplayConnection.Connected: return "Ok";
                case DisplayConnection.MonitorUnavailable: return "Error";
                default: return "Idle";
            }
        }
    }

    // ---- Zeiteingabe -------------------------------------------------------------------------

    public string MinutesInput
    {
        get => _minutesInput;
        set { if (Set(ref _minutesInput, value) && !_syncing) ApplyInputs(); }
    }

    public string SecondsInput
    {
        get => _secondsInput;
        set { if (Set(ref _secondsInput, value) && !_syncing) ApplyInputs(); }
    }

    public string CustomMinutes { get => _customMinutes; set => Set(ref _customMinutes, value); }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set { if (Set(ref _validationMessage, value)) Raise(nameof(HasValidationMessage)); }
    }

    public bool HasValidationMessage => !string.IsNullOrEmpty(_validationMessage);

    public void SyncInputs()
    {
        _syncing = true;
        try
        {
            int total = (int)Math.Round(_timer.Duration.TotalSeconds);
            MinutesInput = (total / 60).ToString(CultureInfo.InvariantCulture);
            SecondsInput = (total % 60).ToString(CultureInfo.InvariantCulture);
            ValidationMessage = "";
        }
        finally { _syncing = false; }
    }

    private bool ApplyInputs()
    {
        if (_timer.IsActive) return true;
        if (!TimeFormatter.TryParseMinutesSeconds(MinutesInput, SecondsInput, out TimeSpan t))
        {
            ValidationMessage = "Ungültige Zeit. Erlaubt sind 00:01 bis 24 Stunden (Minuten und Sekunden als Zahlen).";
            return false;
        }
        ValidationMessage = "";
        _timer.SetDuration(t);
        _settings.DurationSeconds = (int)t.TotalSeconds;
        _requestSave();
        Refresh();
        return true;
    }

    private void Start()
    {
        if (!_timer.IsActive && !ApplyInputs()) return;
        _session.Start();
    }

    private static int ParseMinutes(object p) =>
        p is int i ? i : int.TryParse(p?.ToString(), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private void SetPreset(int minutes)
    {
        if (minutes <= 0 || _timer.IsActive) return;
        _timer.SetDuration(TimeSpan.FromMinutes(minutes));
        _settings.DurationSeconds = minutes * 60;
        SyncInputs();
        _requestSave();
        Refresh();
    }

    private void Adjust(int minutes)
    {
        if (minutes == 0) return;
        if (_session.Adjust(TimeSpan.FromMinutes(minutes)))
        {
            if (_timer.State == TimerState.Ready) { _settings.DurationSeconds = (int)_timer.Duration.TotalSeconds; _requestSave(); }
            SyncInputs();
        }
    }

    private void AdjustCustom(int sign)
    {
        if (!double.TryParse(CustomMinutes?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double m) || m <= 0 || m > 1440)
        {
            ValidationMessage = "Bitte eine gültige Minutenzahl (z. B. 2 oder 2,5) für die Anpassung eingeben.";
            return;
        }
        ValidationMessage = "";
        if (_session.Adjust(TimeSpan.FromMinutes(sign * m)))
        {
            if (_timer.State == TimerState.Ready) { _settings.DurationSeconds = (int)_timer.Duration.TotalSeconds; _requestSave(); }
            SyncInputs();
        }
    }

    public void Refresh()
    {
        Raise(nameof(State));
        Raise(nameof(RemainingText));
        Raise(nameof(DurationText));
        Raise(nameof(EndTimeText));
        Raise(nameof(IsRunning));
        Raise(nameof(IsPaused));
        Raise(nameof(IsActive));
        Raise(nameof(IsFinished));
        Raise(nameof(CanEditDuration));
        Raise(nameof(IsTestOpen));
        Raise(nameof(Progress));
        Raise(nameof(StatusText));
        Raise(nameof(StatusLevel));
        Raise(nameof(DisplayStatusText));
        Raise(nameof(DisplayStatusLevel));
        CommandManager.InvalidateRequerySuggested();
    }
}
