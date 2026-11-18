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

namespace BreakTimer.Core.Util;

public static class TimerLimits
{
    public const int MinSeconds = 1;
    public const int MaxSeconds = 24 * 3600;
}

public static class TimeFormatter
{
    /// <summary>Ganze Sekunden, aufgerundet (der Countdown zeigt erst bei exakt 0 "00:00").</summary>
    public static int CeilSeconds(TimeSpan t) => t <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(t.TotalSeconds - 1e-9);

    /// <summary>MM:SS, ab einer Stunde H:MM:SS.</summary>
    public static string Format(TimeSpan t)
    {
        int total = CeilSeconds(t);
        int h = total / 3600, m = total % 3600 / 60, s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    /// <summary>Uhrzeit auf die nächste volle Minute gerundet (HH:mm).</summary>
    public static string FormatClock(DateTime time) => time.AddSeconds(30).ToString("HH:mm");

    /// <summary>Liest Minuten- und Sekundenfeld; leere Felder zählen als 0. Gültig sind 1 s bis 24 h.</summary>
    public static bool TryParseMinutesSeconds(string minutes, string seconds, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        int m = 0, s = 0;
        if (!string.IsNullOrWhiteSpace(minutes) && !int.TryParse(minutes.Trim(), out m)) return false;
        if (!string.IsNullOrWhiteSpace(seconds) && !int.TryParse(seconds.Trim(), out s)) return false;
        if (m < 0 || s < 0) return false;
        long total = (long)m * 60 + s;
        if (total < TimerLimits.MinSeconds || total > TimerLimits.MaxSeconds) return false;
        result = TimeSpan.FromSeconds(total);
        return true;
    }
}
