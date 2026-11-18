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
using BreakTimer.App.ViewModels;
using BreakTimer.App.Views;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

/// <summary>Zeigt die Mini-Steuerung, solange das Break Display auf dem Monitor des Control Centers läuft.</summary>
public sealed class MiniControlPresenter
{
    private readonly DisplayWindowManager _display;
    private readonly TimerViewModel _timer;
    private MiniControlWindow _window;

    public MiniControlPresenter(DisplayWindowManager display, TimerViewModel timer)
    {
        _display = display;
        _timer = timer;
        display.StateChanged += (s, e) => Update();
    }

    private void Update()
    {
        try
        {
            bool need = _display.IsOpen && _display.SharesWithControl && _display.CurrentMonitor != null;
            if (!need) { Hide(); return; }

            if (_window == null)
            {
                _window = new MiniControlWindow(_timer);
                _window.Closed += (s, e) => _window = null;
            }
            var m = _display.CurrentMonitor;
            int w = (int)(470 * m.DpiScale), h = (int)(132 * m.DpiScale), margin = (int)(28 * m.DpiScale);
            var rect = new PixelRect(m.WorkingArea.Right - w - margin, m.WorkingArea.Bottom - h - margin, w, h);
            WindowPlacement.SetBounds(_window, rect, topmost: true, activate: false);
            if (!_window.IsVisible) _window.Show();
        }
        catch (Exception ex) { Logger.Error("Mini-Steuerung konnte nicht angezeigt werden", ex); }
    }

    /// <summary>Holt die Mini-Steuerung in den Vordergrund (globales Tastenkürzel). False, wenn sie nicht angezeigt wird.</summary>
    public bool BringToFront()
    {
        try
        {
            if (_window == null || !_window.IsVisible) return false;
            _window.Topmost = false; _window.Topmost = true;
            _window.Activate();
            return true;
        }
        catch (Exception ex) { Logger.Error("Mini-Steuerung konnte nicht nach vorn geholt werden", ex); return false; }
    }

    private void Hide()
    {
        if (_window == null) return;
        try { _window.Close(); } catch { }
        _window = null;
    }
}
