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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BreakTimer.Core.Monitoring;

namespace BreakTimer.App.Services;

/// <summary>Blendet kurz die Monitornummer auf jedem Bildschirm ein ("Monitore identifizieren").</summary>
public static class MonitorIdentifier
{
    public static void Show(IEnumerable<MonitorInfo> monitors)
    {
        var windows = new List<Window>();
        foreach (MonitorInfo m in monitors)
        {
            try
            {
                var w = new Window
                {
                    WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
                    Topmost = true, Background = new SolidColorBrush(Color.FromArgb(235, 12, 6, 28)), Width = 260, Height = 200,
                    Content = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = m.Number.ToString(), FontSize = 96, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = m.Resolution, FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(24, 230, 255)), HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    }
                };
                WindowPlacement.EnsureHandle(w);
                WindowPlacement.SetBounds(w, new Core.Util.PixelRect(m.Bounds.X + 40, m.Bounds.Y + 40, (int)(260 * m.DpiScale), (int)(200 * m.DpiScale)), true, false);
                w.Show();
                windows.Add(w);
            }
            catch { /* Hinweisfenster sind optional */ }
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            foreach (Window w in windows) { try { w.Close(); } catch { } }
        };
        timer.Start();
    }
}
