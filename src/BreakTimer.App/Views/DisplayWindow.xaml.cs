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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BreakTimer.App.Native;
using BreakTimer.App.Services;
using BreakTimer.App.ViewModels;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Views;

/// <summary>
/// Vollbild-Anzeigefenster ohne Rahmen und ohne Bedienelemente. Position und Größe werden über einen
/// WM_WINDOWPOSCHANGING-Hook fest auf den Zielmonitor gesperrt (verhindert Verschieben und DPI-Sprünge).
/// </summary>
public partial class DisplayWindow : Window
{
    private readonly DisplayViewModel _vm;
    private PixelRect _bounds;
    private bool _topmost = true;
    private bool _hasBounds;
    private bool _closing;

    public DisplayWindow(DisplayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Wird ausgelöst, wenn im Testmodus Escape gedrückt wurde.</summary>
    public event EventHandler EscapePressed;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this);
        source?.AddHook(WndProc);
        Apply();
    }

    public void SetTarget(PixelRect bounds, bool topmost)
    {
        _bounds = bounds;
        _topmost = topmost;
        _hasBounds = true;
        Topmost = topmost;
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero) Apply();
    }

    private void Apply()
    {
        if (!_hasBounds) return;
        WindowPlacement.SetBounds(this, _bounds, _topmost, activate: false);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING && _hasBounds)
        {
            var pos = (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(NativeMethods.WINDOWPOS));
            pos.x = _bounds.X; pos.y = _bounds.Y; pos.cx = _bounds.Width; pos.cy = _bounds.Height;
            pos.flags &= ~(NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
            Marshal.StructureToPtr(pos, lParam, false);
        }
        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Apply();
        FadeCurtain(0, TimeSpan.FromMilliseconds(700));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Background);
    }

    private bool UseTransitions => _vm.AnimationsEnabled && _vm.Theme.TransitionEffects;

    private void FadeCurtain(double to, TimeSpan duration, Action completed = null)
    {
        if (!UseTransitions)
        {
            Curtain.BeginAnimation(OpacityProperty, null);
            Curtain.Opacity = to;
            completed?.Invoke();
            return;
        }
        var anim = new DoubleAnimation(to, duration) { EasingFunction = new QuadraticEase() };
        if (completed != null) anim.Completed += (s, e) => completed();
        Curtain.BeginAnimation(OpacityProperty, anim);
    }

    /// <summary>Schließt das Fenster; blendet bei aktivierten Übergängen kurz aus. Schließt in jedem Fall nach spätestens 600 ms.</summary>
    public void CloseAnimated()
    {
        if (_closing) return;
        _closing = true;
        try
        {
            if (UseTransitions)
            {
                FadeCurtain(1, TimeSpan.FromMilliseconds(350), ForceClose);
                var guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                guard.Tick += (s, e) => { guard.Stop(); ForceClose(); };
                guard.Start();
            }
            else ForceClose();
        }
        catch { ForceClose(); }
    }

    private void ForceClose()
    {
        try { Close(); } catch { /* bereits geschlossen */ }
    }

    public bool EscapeEnabled { get; set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EscapeEnabled) { EscapePressed?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    }

    // Verhindert, dass ein Klick das Fenster in den Vordergrund über das Control Center hebt.
    protected override void OnMouseDown(MouseButtonEventArgs e) { e.Handled = true; }
}
