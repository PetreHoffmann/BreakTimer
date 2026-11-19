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
using System.Windows.Threading;
using BreakTimer.App.Native;
using BreakTimer.Core.Models;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

/// <summary>
/// Hält Monitor und PC wach, solange das Break Display offen ist (und die Einstellung aktiv ist).
/// Nutzt SetThreadExecutionState; der Zustand gilt pro Thread, deshalb laufen alle Aufrufe auf dem UI-Thread.
/// </summary>
public sealed class KeepAwakeService
{
    private readonly DisplayWindowManager _display;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private bool _active;

    public KeepAwakeService(DisplayWindowManager display, AppSettings settings)
    {
        _display = display;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
        display.StateChanged += (s, e) => Update();
    }

    public bool IsActive => _active;

    /// <summary>Gleicht den Windows-Zustand mit Display-Status und Einstellung ab.</summary>
    public void Update()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(Update)); return; }
        bool need = _settings.KeepAwakeDuringBreak && _display.IsOpen;
        if (need == _active) return;
        try
        {
            uint flags = need
                ? NativeMethods.ES_CONTINUOUS | NativeMethods.ES_DISPLAY_REQUIRED | NativeMethods.ES_SYSTEM_REQUIRED
                : NativeMethods.ES_CONTINUOUS;
            if (NativeMethods.SetThreadExecutionState(flags) == 0)
                Logger.Error("SetThreadExecutionState fehlgeschlagen");
            else
            {
                _active = need;
                Logger.Info(need ? "Wach-halten aktiv (Break Display offen)" : "Wach-halten beendet");
            }
        }
        catch (Exception ex) { Logger.Error("Wach-halten konnte nicht gesetzt werden", ex); }
    }

    /// <summary>Gibt die Anforderung beim Beenden der Anwendung frei.</summary>
    public void Release()
    {
        try { NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS); } catch { }
        _active = false;
    }
}
