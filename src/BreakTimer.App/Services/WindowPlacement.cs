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
using System.Windows.Interop;
using BreakTimer.App.Native;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

/// <summary>Positionierung von WPF-Fenstern in physikalischen Pixeln des virtuellen Desktops (auch bei negativen Koordinaten und gemischter DPI).</summary>
public static class WindowPlacement
{
    public static IntPtr EnsureHandle(Window window) => new WindowInteropHelper(window).EnsureHandle();

    public static void SetBounds(Window window, PixelRect bounds, bool topmost, bool activate)
    {
        IntPtr hwnd = EnsureHandle(window);
        uint flags = NativeMethods.SWP_SHOWWINDOW | (activate ? 0 : NativeMethods.SWP_NOACTIVATE);
        NativeMethods.SetWindowPos(hwnd, topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, flags);
    }

    /// <summary>Zentriert ein Fenster mit der Wunschgröße (in DIP bei 100 % = Pixel auf einem 96-dpi-Monitor) im Arbeitsbereich des Monitors.</summary>
    public static PixelRect CenterOnMonitor(MonitorInfo monitor, double widthDip, double heightDip)
    {
        int w = (int)Math.Round(widthDip * monitor.DpiScale);
        int h = (int)Math.Round(heightDip * monitor.DpiScale);
        return monitor.WorkingArea.Center(w, h);
    }

    /// <summary>Verschiebt ein (ggf. noch nicht sichtbares) normales Fenster zentriert auf den Monitor.</summary>
    public static void MoveToMonitor(Window window, MonitorInfo monitor, double widthDip, double heightDip)
    {
        IntPtr hwnd = EnsureHandle(window);
        PixelRect r = CenterOnMonitor(monitor, widthDip, heightDip);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public static PixelRect GetBounds(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT r)) return new PixelRect(0, 0, 0, 0);
        return new PixelRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
