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
using System.Diagnostics;
using System.Threading;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Timer;

public enum TimerState { Ready, Running, Paused, Finished, Stopped }

/// <summary>Monotone Zeitquelle (austauschbar für Tests).</summary>
public interface IClock
{
    TimeSpan Elapsed { get; }
}

public sealed class StopwatchClock : IClock
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public TimeSpan Elapsed => _sw.Elapsed;
}

public sealed class TimerStateChangedEventArgs : EventArgs
{
    public TimerStateChangedEventArgs(TimerState oldState, TimerState newState) { OldState = oldState; NewState = newState; }
    public TimerState OldState { get; }
    public TimerState NewState { get; }
}

/// <summary>
/// Zuverlässiger Countdown. Die Restzeit wird immer aus der tatsächlich vergangenen Zeit einer
/// monotonen Uhr (Stopwatch) berechnet - nie durch Herunterzählen von Tick-Ereignissen.
/// Der Service ist unabhängig von der Oberfläche; Ereignisse werden optional über einen
/// SynchronizationContext (UI-Thread) ausgeliefert. Es gibt genau eine Instanz pro Anwendung.
/// </summary>
public sealed class TimerService : IDisposable
{
    private readonly object _gate = new object();
    private readonly IClock _clock;
    private readonly SynchronizationContext _context;
    private readonly System.Threading.Timer _ticker;

    private TimerState _state = TimerState.Ready;
    private TimeSpan _duration = TimeSpan.FromMinutes(15);
    private TimeSpan _remainingAtMark;   // Restzeit zum Zeitpunkt _mark
    private TimeSpan _mark;              // Uhrstand beim letzten Start/Fortsetzen/Anpassen
    private int _lastTickSecond = -1;
    private TimerState _lastReportedState = TimerState.Ready;
    private int _finishedRaised;

    public TimerService(IClock clock = null, SynchronizationContext context = null, bool autoTick = true, int tickIntervalMs = 100)
    {
        _clock = clock ?? new StopwatchClock();
        _context = context;
        _remainingAtMark = _duration;
        if (autoTick) _ticker = new System.Threading.Timer(_ => Poll(), null, tickIntervalMs, tickIntervalMs);
    }

    /// <summary>Anzeige-relevante Änderung der Restzeit (mindestens einmal pro Sekunde, sowie bei jeder Anpassung).</summary>
    public event EventHandler Tick;
    public event EventHandler<TimerStateChangedEventArgs> StateChanged;
    /// <summary>Countdown hat 00:00 erreicht.</summary>
    public event EventHandler Finished;

    public TimerState State { get { lock (_gate) return _state; } }

    /// <summary>Eingestellte Ausgangszeit.</summary>
    public TimeSpan Duration { get { lock (_gate) return _duration; } }

    /// <summary>Verbleibende Zeit; nie negativ. Im Zustand Ready entspricht sie der Ausgangszeit.</summary>
    public TimeSpan Remaining { get { lock (_gate) return RemainingLocked(); } }

    /// <summary>Voraussichtliches Ende, wenn der Countdown jetzt läuft bzw. fortgesetzt wird.</summary>
    public DateTime ExpectedEnd => DateTime.Now + Remaining;

    public bool IsActive { get { var s = State; return s == TimerState.Running || s == TimerState.Paused; } }

    public bool CanStart { get { lock (_gate) return (_state == TimerState.Ready || _state == TimerState.Stopped || _state == TimerState.Finished) && ValidDuration(_duration); } }
    public bool CanPause => State == TimerState.Running;
    public bool CanResume => State == TimerState.Paused;
    public bool CanStop => IsActive;
    public bool CanAdjust { get { var s = State; return s == TimerState.Running || s == TimerState.Paused || s == TimerState.Ready; } }

    public static bool ValidDuration(TimeSpan d) => d.TotalSeconds >= TimerLimits.MinSeconds && d.TotalSeconds <= TimerLimits.MaxSeconds;

    private TimeSpan RemainingLocked()
    {
        TimeSpan r = _state == TimerState.Running ? _remainingAtMark - (_clock.Elapsed - _mark) : _remainingAtMark;
        return r < TimeSpan.Zero ? TimeSpan.Zero : r;
    }

    /// <summary>Setzt die Ausgangszeit (nur wenn kein Countdown aktiv ist). Wechselt Finished/Stopped nach Ready.</summary>
    public bool SetDuration(TimeSpan duration)
    {
        bool changed;
        lock (_gate)
        {
            if (_state == TimerState.Running || _state == TimerState.Paused) return false;
            if (!ValidDuration(duration)) return false;
            _duration = TimeSpan.FromSeconds(Math.Round(duration.TotalSeconds));
            _remainingAtMark = _duration;
            _state = TimerState.Ready;
            changed = true;
        }
        if (changed) { RaiseState(); RaiseTick(true); }
        return changed;
    }

    public bool Start()
    {
        lock (_gate)
        {
            if (!(_state == TimerState.Ready || _state == TimerState.Stopped || _state == TimerState.Finished)) return false;
            if (!ValidDuration(_duration)) return false;
            _remainingAtMark = _duration;
            _mark = _clock.Elapsed;
            _finishedRaised = 0;
            _state = TimerState.Running;
        }
        RaiseState(); RaiseTick(true);
        return true;
    }

    public bool Pause()
    {
        lock (_gate)
        {
            if (_state != TimerState.Running) return false;
            _remainingAtMark = RemainingLocked();
            _state = TimerState.Paused;
        }
        RaiseState(); RaiseTick(true);
        return true;
    }

    public bool Resume()
    {
        lock (_gate)
        {
            if (_state != TimerState.Paused) return false;
            _mark = _clock.Elapsed;
            _state = TimerState.Running;
        }
        RaiseState(); RaiseTick(true);
        return true;
    }

    /// <summary>Beendet den laufenden oder pausierten Countdown (Zustand Stopped).</summary>
    public bool Stop()
    {
        lock (_gate)
        {
            if (_state != TimerState.Running && _state != TimerState.Paused) return false;
            _remainingAtMark = RemainingLocked();
            _state = TimerState.Stopped;
        }
        RaiseState(); RaiseTick(true);
        return true;
    }

    /// <summary>Setzt auf die eingestellte Ausgangszeit zurück (Zustand Ready).</summary>
    public bool Reset()
    {
        lock (_gate)
        {
            _remainingAtMark = _duration;
            _state = TimerState.Ready;
            _finishedRaised = 0;
        }
        RaiseState(); RaiseTick(true);
        return true;
    }

    /// <summary>Verlängert (positiv) oder verkürzt (negativ) die Restzeit. Im Zustand Ready ändert es die Ausgangszeit.</summary>
    public bool Adjust(TimeSpan delta)
    {
        bool finishNow = false;
        lock (_gate)
        {
            switch (_state)
            {
                case TimerState.Ready:
                    {
                        TimeSpan d = _duration + delta;
                        if (d.TotalSeconds < TimerLimits.MinSeconds) d = TimeSpan.FromSeconds(TimerLimits.MinSeconds);
                        if (d.TotalSeconds > TimerLimits.MaxSeconds) d = TimeSpan.FromSeconds(TimerLimits.MaxSeconds);
                        _duration = d;
                        _remainingAtMark = d;
                        break;
                    }
                case TimerState.Running:
                case TimerState.Paused:
                    {
                        TimeSpan cur = RemainingLocked();
                        TimeSpan next = cur + delta;
                        if (next.TotalSeconds > TimerLimits.MaxSeconds) next = TimeSpan.FromSeconds(TimerLimits.MaxSeconds);
                        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
                        _remainingAtMark = next;
                        _mark = _clock.Elapsed;
                        if (_state == TimerState.Running && next <= TimeSpan.Zero) finishNow = true;
                        break;
                    }
                default:
                    return false;
            }
        }
        RaiseTick(true);
        if (finishNow) Poll();
        return true;
    }

    /// <summary>Prüft Ablauf und meldet Änderungen. Wird vom internen Ticker aufgerufen (in Tests manuell).</summary>
    public void Poll()
    {
        bool finished = false;
        lock (_gate)
        {
            if (_state == TimerState.Running && RemainingLocked() <= TimeSpan.Zero)
            {
                _remainingAtMark = TimeSpan.Zero;
                _state = TimerState.Finished;
                finished = true;
            }
        }
        if (finished && Interlocked.Exchange(ref _finishedRaised, 1) == 0)
        {
            RaiseState();
            RaiseTick(true);
            Post(() => Finished?.Invoke(this, EventArgs.Empty));
            return;
        }
        RaiseTick(false);
    }

    private void RaiseTick(bool force)
    {
        int sec = TimeFormatter.CeilSeconds(Remaining);
        if (!force)
        {
            if (sec == _lastTickSecond) return;
        }
        _lastTickSecond = sec;
        Post(() => Tick?.Invoke(this, EventArgs.Empty));
    }

    private void RaiseState()
    {
        TimerState oldS, newS;
        lock (_gate)
        {
            oldS = _lastReportedState;
            newS = _state;
            _lastReportedState = newS;
        }
        if (oldS == newS) return;
        Post(() => StateChanged?.Invoke(this, new TimerStateChangedEventArgs(oldS, newS)));
    }

    private void Post(Action action)
    {
        if (_context == null || SynchronizationContext.Current == _context) action();
        else _context.Post(_ => action(), null);
    }

    public void Dispose() => _ticker?.Dispose();
}
