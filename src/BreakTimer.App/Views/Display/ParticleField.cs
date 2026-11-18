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
using System.Windows.Media;

namespace BreakTimer.App.Views.Display;

/// <summary>Ressourcenschonendes Partikelfeld (ca. 30 Bilder/s, nur aktiv wenn eingeschaltet und sichtbar).</summary>
public sealed class ParticleField : FrameworkElement
{
    private const int Count = 64;
    private readonly double[] _x = new double[Count], _y = new double[Count], _vx = new double[Count], _vy = new double[Count];
    private readonly double[] _r = new double[Count], _phase = new double[Count];
    private readonly Random _rnd = new Random(7);
    private readonly Brush[] _brushes = new Brush[8];
    private TimeSpan _last;
    private double _time;
    private bool _running;

    public ParticleField()
    {
        IsHitTestVisible = false;
        for (int i = 0; i < Count; i++) Reset(i, true);
        SetColor(Colors.White);
    }

    public void SetColor(Color c)
    {
        for (int i = 0; i < _brushes.Length; i++)
        {
            var b = new SolidColorBrush(Color.FromArgb((byte)(40 + i * 28), c.R, c.G, c.B));
            b.Freeze();
            _brushes[i] = b;
        }
        if (!_running) InvalidateVisual();
    }

    private void Reset(int i, bool anywhere)
    {
        _x[i] = _rnd.NextDouble();
        _y[i] = anywhere ? _rnd.NextDouble() : 1.02;
        _vx[i] = (_rnd.NextDouble() - 0.5) * 0.006;
        _vy[i] = -(0.004 + _rnd.NextDouble() * 0.012);
        _r[i] = 1.0 + _rnd.NextDouble() * 2.6;
        _phase[i] = _rnd.NextDouble() * Math.PI * 2;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _last = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
        Visibility = Visibility.Visible;
    }

    public void Stop()
    {
        if (!_running) { Visibility = Visibility.Collapsed; return; }
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        Visibility = Visibility.Collapsed;
    }

    private void OnRendering(object sender, EventArgs e)
    {
        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        if (_last == TimeSpan.Zero) { _last = now; return; }
        double dt = (now - _last).TotalSeconds;
        if (dt < 0.033) return;        // ~30 FPS genügt für dezente Bewegung
        _last = now;
        dt = Math.Min(dt, 0.1);
        _time += dt;
        for (int i = 0; i < Count; i++)
        {
            _x[i] += _vx[i] * dt * 6 + Math.Sin(_time * 0.6 + _phase[i]) * 0.0004;
            _y[i] += _vy[i] * dt * 6;
            if (_y[i] < -0.03 || _x[i] < -0.03 || _x[i] > 1.03) Reset(i, false);
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double scale = Math.Max(0.5, w / 1920.0);
        for (int i = 0; i < Count; i++)
        {
            double tw = 0.5 + 0.5 * Math.Sin(_time * 1.3 + _phase[i]);
            int level = Math.Min(_brushes.Length - 1, (int)(tw * (_brushes.Length - 1) * Math.Min(1, 0.4 + _y[i] * 0.9) + 0.5));
            double r = _r[i] * scale;
            dc.DrawEllipse(_brushes[Math.Max(0, level)], null, new Point(_x[i] * w, _y[i] * h), r, r);
        }
    }
}
