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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using BreakTimer.App.Services;
using BreakTimer.App.ViewModels;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Views;

/// <summary>Control Center: Fensterplatzierung auf dem Steuerungsmonitor und Tastenkürzel. Die Logik liegt in den ViewModels.</summary>
public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly MainViewModel _vm;
    private readonly MonitorService _monitors;
    private readonly Action<string> _controlMonitorChanged;
    private readonly Action _shutdown;
    private readonly DispatcherTimer _moveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    private const int HotkeyId = 0xB7E1;
    private const int WM_HOTKEY = 0x0312;
    private bool _hotkeyRegistered;
    private bool _programmatic;

    /// <summary>Wird aufgerufen, wenn das globale Tastenkürzel Strg+Alt+B gedrückt wurde (auch ohne Fokus).</summary>
    public Action GlobalHotkeyPressed { get; set; }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private bool _closingHandled;

    public MainWindow(MainViewModel vm, MonitorService monitors, Action<string> controlMonitorChanged, Action shutdown)
    {
        InitializeComponent();
        _vm = vm;
        _monitors = monitors;
        _controlMonitorChanged = controlMonitorChanged;
        _shutdown = shutdown;
        DataContext = vm;

        PreviewKeyDown += OnPreviewKeyDown;
        _moveDebounce.Tick += (s, e) => { _moveDebounce.Stop(); DetectUserMove(); };
        LocationChanged += (s, e) => { if (!_programmatic) { _moveDebounce.Stop(); _moveDebounce.Start(); } };
        monitors.SelectionChanged += (s, e) => PlaceOnControlMonitor(false);
        monitors.MonitorsChanged += (s, e) => PlaceOnControlMonitor(false);
        Closing += OnClosing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Dunkle Titelleiste (Windows 10 20H1+ / Windows 11), Fehler werden ignoriert.
        try
        {
            IntPtr h = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, sizeof(int));
            DwmSetWindowAttribute(h, 19, ref on, sizeof(int));
            int caption = 0x1E0A0F;  // COLORREF 0x00BBGGRR = #0F0A1E
            DwmSetWindowAttribute(h, 35, ref caption, sizeof(int));
        }
        catch { }
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(HotkeyHook);
            // MOD_ALT | MOD_CONTROL | MOD_NOREPEAT, B
            _hotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, 0x0001 | 0x0002 | 0x4000, 0x42);
            if (!_hotkeyRegistered) Logger.Info("Globales Tastenkürzel Strg+Alt+B konnte nicht registriert werden (bereits belegt?)");
        }
        catch (Exception ex) { Logger.Error("Tastenkürzel-Registrierung fehlgeschlagen", ex); }
        PlaceOnControlMonitor(true);
    }

    private IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            try { GlobalHotkeyPressed?.Invoke(); } catch (Exception ex) { Logger.Error("Tastenkürzel-Aktion fehlgeschlagen", ex); }
        }
        return IntPtr.Zero;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        PlaceOnControlMonitor(true);
    }

    /// <summary>Verschiebt das Control Center auf den gewählten Steuerungsmonitor (Standard: Hauptmonitor).</summary>
    private void PlaceOnControlMonitor(bool force)
    {
        try
        {
            MonitorInfo target = _monitors.ResolveControl();
            if (target == null) return;
            MonitorInfo current = CurrentMonitor();
            if (!force && current != null && current.Id == target.Id) return;
            _programmatic = true;
            WindowPlacement.MoveToMonitor(this, target, 1320, 860);
        }
        catch (Exception ex) { Logger.Error("Control Center konnte nicht platziert werden", ex); }
        finally { Dispatcher.BeginInvoke(new Action(() => _programmatic = false), DispatcherPriority.ApplicationIdle); }
    }

    private MonitorInfo CurrentMonitor()
    {
        PixelRect r = WindowPlacement.GetBounds(this);
        if (r.Width == 0) return null;
        return _monitors.FromPoint(r.CenterX, r.CenterY);
    }

    /// <summary>Wurde das Fenster vom Benutzer auf einen anderen Monitor gezogen, wird dieser zum Steuerungsmonitor.</summary>
    private void DetectUserMove()
    {
        MonitorInfo current = CurrentMonitor();
        if (current == null) return;
        MonitorInfo expected = _monitors.ResolveControl();
        if (expected != null && current.Id != expected.Id) _controlMonitorChanged(current.Id);
    }

    // ---- Tastenkürzel ------------------------------------------------------------------------

    private static bool IsTextInput(object element) =>
        element is TextBoxBase || element is PasswordBox || (element is ComboBox cb && cb.IsEditable);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsActive) return;
        object focus = Keyboard.FocusedElement;
        bool typing = IsTextInput(focus);
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool plain = Keyboard.Modifiers == ModifierKeys.None;
        TimerViewModel t = _vm.Timer;

        // Offene Auswahllisten oder Popups haben Vorrang vor Esc.
        if (e.Key == Key.Escape && (focus is ComboBox c && c.IsDropDownOpen || focus is ToggleButton tb && tb.IsChecked == true && !(focus is RadioButton)))
            return;

        if (ctrl && e.Key == Key.S) { _vm.SaveNow(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.R && !typing) { t.ResetCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Escape && plain) { t.StopCommand.Execute(null); e.Handled = true; return; }
        if (typing || !plain && e.Key != Key.OemPlus) return;

        switch (e.Key)
        {
            case Key.Space:
                t.Session.StartOrToggle();
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                t.AdjustCommand.Execute(1);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                t.AdjustCommand.Execute(-1);
                e.Handled = true;
                break;
        }
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        try { if (_hotkeyRegistered) UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId); } catch { }
        if (_closingHandled) return;
        _closingHandled = true;
        try { _shutdown(); } catch (Exception ex) { Logger.Error("Fehler beim Beenden", ex); }
    }
}
