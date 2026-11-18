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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Monitoring;

public interface IMonitorProvider
{
    IList<MonitorInfo> GetMonitors();
}

/// <summary>Liest die Monitore über System.Windows.Forms.Screen, Anzeigenamen über EnumDisplayDevices und DPI über shcore.</summary>
public sealed class ScreenMonitorProvider : IMonitorProvider
{
    public IList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        int index = 0;
        foreach (Screen s in Screen.AllScreens)
        {
            index++;
            var info = new MonitorInfo
            {
                Id = s.DeviceName,
                Number = index,
                IsPrimary = s.Primary,
                Bounds = new PixelRect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                WorkingArea = new PixelRect(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height),
                FriendlyName = GetFriendlyName(s.DeviceName),
                DpiScale = GetDpiScale(s)
            };
            list.Add(info);
        }
        return list;
    }

    private static int ParseNumber(string deviceName, int fallback)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            int i = deviceName.Length;
            while (i > 0 && char.IsDigit(deviceName[i - 1])) i--;
            if (i < deviceName.Length && int.TryParse(deviceName.Substring(i), out int n)) return n;
        }
        return fallback;
    }

    // ---- Native ------------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private static string GetFriendlyName(string deviceName)
    {
        try
        {
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
            if (EnumDisplayDevices(deviceName, 0, ref dd, 0) && !string.IsNullOrWhiteSpace(dd.DeviceString))
                return dd.DeviceString.Trim();
        }
        catch (Exception ex) { Logger.Error("EnumDisplayDevices fehlgeschlagen", ex); }
        return "";
    }

    private static double GetDpiScale(Screen s)
    {
        try
        {
            IntPtr hMon = MonitorFromPoint(new POINT { X = s.Bounds.X + s.Bounds.Width / 2, Y = s.Bounds.Y + s.Bounds.Height / 2 }, 2 /*MONITOR_DEFAULTTONEAREST*/);
            if (hMon != IntPtr.Zero && GetDpiForMonitor(hMon, 0 /*MDT_EFFECTIVE_DPI*/, out uint x, out _) == 0 && x > 0)
                return x / 96.0;
        }
        catch (Exception) { /* shcore.dll fehlt (Windows < 8.1) - 100 % annehmen */ }
        return 1.0;
    }
}
