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
using System.Threading;
using BreakTimer.Core.Util;
using Microsoft.Win32;

namespace BreakTimer.Core.Monitoring;

/// <summary>Ergebnis der Zielmonitor-Auflösung.</summary>
public sealed class MonitorResolution
{
    public MonitorInfo Monitor { get; set; }
    /// <summary>true, wenn der gewünschte Monitor nicht (mehr) verfügbar ist und ein Ersatz gewählt wurde.</summary>
    public bool IsFallback { get; set; }
    /// <summary>true, wenn Anzeige- und Steuerungsmonitor identisch sind.</summary>
    public bool SharesWithControl { get; set; }
}

/// <summary>Erkennt Monitore, hält die gewählte Zuordnung (Steuerung/Anzeige) und meldet Änderungen der Hardware-Konfiguration.</summary>
public sealed class MonitorService : IDisposable
{
    private readonly IMonitorProvider _provider;
    private readonly SynchronizationContext _context;
    private System.Threading.Timer _debounce;
    private bool _watching;
    private string _signature = "";
    private IList<MonitorInfo> _monitors = new List<MonitorInfo>();

    public MonitorService(IMonitorProvider provider, SynchronizationContext context = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _context = context;
    }

    public IReadOnlyList<MonitorInfo> Monitors => (IReadOnlyList<MonitorInfo>)_monitors;

    /// <summary>Gewünschter Anzeigemonitor (Id). Leer = automatisch.</summary>
    public string PreferredDisplayId { get; private set; } = "";
    /// <summary>Gewünschter Steuerungsmonitor (Id). Leer = Hauptmonitor.</summary>
    public string PreferredControlId { get; private set; } = "";

    /// <summary>Monitorkonfiguration hat sich geändert (angeschlossen, entfernt, Auflösung, DPI, Position).</summary>
    public event EventHandler MonitorsChanged;
    /// <summary>Zuordnung von Steuerungs-/Anzeigemonitor wurde geändert.</summary>
    public event EventHandler SelectionChanged;

    public bool Refresh()
    {
        IList<MonitorInfo> list;
        try { list = _provider.GetMonitors() ?? new List<MonitorInfo>(); }
        catch (Exception ex)
        {
            Logger.Error("Monitorerkennung fehlgeschlagen", ex);
            return false;
        }
        string sig = string.Join("|", list.Select(m => $"{m.Id};{m.Bounds};{m.WorkingArea};{m.DpiScale:0.00};{m.IsPrimary}"));
        bool changed = sig != _signature;
        _monitors = list;
        _signature = sig;
        return changed;
    }

    /// <summary>Erkennt neu und löst MonitorsChanged aus, falls sich etwas geändert hat.</summary>
    public void RefreshAndNotify()
    {
        if (Refresh()) MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StartWatching()
    {
        if (_watching) return;
        _watching = true;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e)
    {
        // Windows sendet das Ereignis oft mehrfach; kurz bündeln.
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(_ =>
        {
            if (_context != null) _context.Post(__ => RefreshAndNotify(), null);
            else RefreshAndNotify();
        }, null, 700, Timeout.Infinite);
    }

    public void SetPreferred(string displayId, string controlId)
    {
        bool changed = (displayId ?? "") != PreferredDisplayId || (controlId ?? "") != PreferredControlId;
        PreferredDisplayId = displayId ?? "";
        PreferredControlId = controlId ?? "";
        if (changed) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPreferredDisplay(string id) => SetPreferred(id, PreferredControlId);
    public void SetPreferredControl(string id) => SetPreferred(PreferredDisplayId, id);

    public MonitorInfo Find(string id) =>
        string.IsNullOrEmpty(id) ? null : _monitors.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    public MonitorInfo Primary => _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors.FirstOrDefault();

    /// <summary>Steuerungsmonitor: gewählter Monitor, sonst Hauptmonitor.</summary>
    public MonitorInfo ResolveControl() => Find(PreferredControlId) ?? Primary;

    /// <summary>
    /// Anzeigemonitor: gewählter Monitor; ist er nicht verfügbar (oder nichts gewählt), der erste Monitor,
    /// der nicht der Steuerungsmonitor ist; gibt es nur einen Monitor, der Steuerungsmonitor selbst.
    /// </summary>
    public MonitorResolution ResolveDisplay()
    {
        MonitorInfo control = ResolveControl();
        MonitorInfo preferred = Find(PreferredDisplayId);
        MonitorInfo chosen = preferred;
        bool fallback = false;
        if (chosen == null)
        {
            fallback = !string.IsNullOrEmpty(PreferredDisplayId);
            chosen = PickDefaultDisplay(control);
        }
        return new MonitorResolution
        {
            Monitor = chosen,
            IsFallback = fallback,
            SharesWithControl = chosen != null && control != null && string.Equals(chosen.Id, control.Id, StringComparison.OrdinalIgnoreCase)
        };
    }

    private MonitorInfo PickDefaultDisplay(MonitorInfo control)
    {
        MonitorInfo other = _monitors.FirstOrDefault(m => control == null || !string.Equals(m.Id, control.Id, StringComparison.OrdinalIgnoreCase));
        return other ?? control ?? _monitors.FirstOrDefault();
    }

    /// <summary>Monitor, in dem der Punkt (physikalische Pixel) liegt; sonst der nächstgelegene.</summary>
    public MonitorInfo FromPoint(int x, int y)
    {
        MonitorInfo hit = _monitors.FirstOrDefault(m => m.Bounds.Contains(x, y));
        if (hit != null) return hit;
        return _monitors.OrderBy(m => Dist(m.Bounds, x, y)).FirstOrDefault();
    }

    private static long Dist(PixelRect r, int x, int y)
    {
        long dx = x < r.X ? r.X - x : x >= r.Right ? x - r.Right + 1 : 0;
        long dy = y < r.Y ? r.Y - y : y >= r.Bottom ? y - r.Bottom + 1 : 0;
        return dx * dx + dy * dy;
    }

    public void Dispose()
    {
        if (_watching) SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _watching = false;
        _debounce?.Dispose();
    }
}
