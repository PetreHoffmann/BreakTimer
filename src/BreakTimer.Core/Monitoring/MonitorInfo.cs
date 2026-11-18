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

using BreakTimer.Core.Util;

namespace BreakTimer.Core.Monitoring;

/// <summary>Beschreibung eines angeschlossenen Monitors (alle Maße in physikalischen Pixeln).</summary>
public sealed class MonitorInfo
{
    /// <summary>Stabile Kennung (Windows-Gerätename, z. B. \\.\DISPLAY2).</summary>
    public string Id { get; set; }
    public int Number { get; set; }
    public string FriendlyName { get; set; }
    public bool IsPrimary { get; set; }
    public PixelRect Bounds { get; set; }
    public PixelRect WorkingArea { get; set; }
    /// <summary>1.0 = 100 %, 1.5 = 150 % usw.</summary>
    public double DpiScale { get; set; } = 1.0;

    public int Width => Bounds.Width;
    public int Height => Bounds.Height;
    public string Resolution => $"{Width} × {Height}";
    public string DpiText => $"{(int)System.Math.Round(DpiScale * 100)} %";

    /// <summary>Beispiel: "Monitor 2 – Hauptmonitor – 1920 × 1080".</summary>
    public string DisplayName
    {
        get
        {
            string s = $"Monitor {Number}";
            if (IsPrimary) s += " – Hauptmonitor";
            else if (!string.IsNullOrWhiteSpace(FriendlyName) && !FriendlyName.StartsWith("Generic")) s += " – " + FriendlyName;
            return s + " – " + Resolution;
        }
    }

    public override string ToString() => DisplayName;
}
