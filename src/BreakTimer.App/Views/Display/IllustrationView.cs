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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace BreakTimer.App.Views.Display;

/// <summary>
/// Eigene, vektorbasierte Illustrationen (keine fremden Motive): Orbit (futuristisches Polyeder mit Ringen),
/// Grid (Neon-Horizont mit Sonne und Skyline), Nature (Hügel, Mond, Blätter) und Corporate (geometrische Komposition).
/// </summary>
public sealed class IllustrationView : DisplayControlBase
{
    private const double S = 600;          // Zeichenfläche S x S
    private readonly Viewbox _box = new Viewbox { Stretch = Stretch.Uniform };
    private readonly Canvas _canvas = new Canvas { Width = S, Height = S };
    private string _signature;
    private RotateTransform _spin;

    public IllustrationView()
    {
        IsHitTestVisible = false;
        _box.Child = _canvas;
        Content = _box;
    }

    protected override void OnDetached()
    {
        _spin?.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    protected override void Refresh(string p)
    {
        if (Theme == null) return;
        var t = Theme;
        string sig = string.Join("|", t.IllustrationStyle, t.AccentColor, t.SecondaryColor, t.TextColor, t.GlowEnabled, t.GlowIntensity, Animations, t.IllustrationSpeed);
        if (sig == _signature) return;
        _signature = sig;
        Build();
    }

    private void Build()
    {
        _canvas.Children.Clear();
        _spin = null;
        Color a = Col(Theme.AccentColor, Colors.Cyan);
        Color b = Col(Theme.SecondaryColor, Colors.Magenta);
        Color tx = Col(Theme.TextColor, Colors.White);
        switch (Theme.IllustrationStyle)
        {
            case "Grid": BuildGrid(a, b, tx); break;
            case "Nature": BuildNature(a, b, tx); break;
            case "Corporate": BuildCorporate(a, b, tx); break;
            case "None": break;
            default: BuildOrbit(a, b, tx); break;
        }
        if (Theme.GlowEnabled && Theme.IllustrationStyle != "None")
            _canvas.Effect = new DropShadowEffect { Color = Col(Theme.GlowColor, a), BlurRadius = 8 + Theme.GlowIntensity * 0.28, ShadowDepth = 0, Opacity = 0.55 };
        else _canvas.Effect = null;
    }

    // ---- Helfer ------------------------------------------------------------------------------

    private static Brush Alpha(Color c, double a) => new SolidColorBrush(Color.FromArgb((byte)(255 * a), c.R, c.G, c.B));

    private static Brush Radial(Color c, double a0, double a1 = 0) =>
        new RadialGradientBrush(Color.FromArgb((byte)(255 * a0), c.R, c.G, c.B), Color.FromArgb((byte)(255 * a1), c.R, c.G, c.B));

    private Ellipse Circle(double cx, double cy, double rx, double ry, Brush fill, Brush stroke, double thickness = 0)
    {
        var e = new Ellipse { Width = rx * 2, Height = ry * 2, Fill = fill, Stroke = stroke, StrokeThickness = thickness };
        Canvas.SetLeft(e, cx - rx); Canvas.SetTop(e, cy - ry);
        _canvas.Children.Add(e);
        return e;
    }

    private Polygon Poly(Brush fill, Brush stroke, double thickness, params double[] xy)
    {
        var p = new Polygon { Fill = fill, Stroke = stroke, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
        for (int i = 0; i < xy.Length; i += 2) p.Points.Add(new Point(xy[i], xy[i + 1]));
        _canvas.Children.Add(p);
        return p;
    }

    private Line Ln(double x1, double y1, double x2, double y2, Brush stroke, double thickness)
    {
        var l = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        _canvas.Children.Add(l);
        return l;
    }

    private Path Geo(string data, Brush fill, Brush stroke, double thickness = 0)
    {
        var p = new Path { Data = Geometry.Parse(data), Fill = fill, Stroke = stroke, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
        _canvas.Children.Add(p);
        return p;
    }

    // ---- Orbit -------------------------------------------------------------------------------

    private void BuildOrbit(Color a, Color b, Color tx)
    {
        Circle(300, 300, 290, 290, Radial(a, 0.28), null);

        var rings = new Canvas { Width = S, Height = S };
        _spin = new RotateTransform(0, 300, 300);
        rings.RenderTransform = _spin;
        _canvas.Children.Add(rings);
        void Ring(double rx, double ry, double angle, Brush stroke, double th)
        {
            var e = new Ellipse { Width = rx * 2, Height = ry * 2, Stroke = stroke, StrokeThickness = th, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(angle) };
            Canvas.SetLeft(e, 300 - rx); Canvas.SetTop(e, 300 - ry);
            rings.Children.Add(e);
        }
        Ring(280, 280, 0, Alpha(a, 0.75), 2.5);
        Ring(250, 95, 28, Alpha(b, 0.85), 3);
        Ring(250, 95, -38, Alpha(a, 0.6), 2);
        Ring(205, 205, 0, Alpha(tx, 0.18), 1.5);
        foreach (var pt in new[] { new Point(300, 20), new Point(568, 395), new Point(78, 470) })
        {
            var d = new Ellipse { Width = 18, Height = 18, Fill = Alpha(Colors.White, 0.95) };
            Canvas.SetLeft(d, pt.X - 9); Canvas.SetTop(d, pt.Y - 9);
            rings.Children.Add(d);
        }

        // Polyeder (isometrischer Kristall)
        Poly(Alpha(a, 0.28), Alpha(tx, 0.9), 3, 300, 185, 400, 243, 300, 300, 200, 243);
        Poly(Alpha(b, 0.34), Alpha(tx, 0.9), 3, 200, 243, 300, 300, 300, 415, 200, 357);
        Poly(Alpha(a, 0.12), Alpha(tx, 0.9), 3, 300, 300, 400, 243, 400, 357, 300, 415);
        Ln(300, 185, 300, 120, Alpha(a, 0.9), 3);
        Circle(300, 112, 9, 9, Alpha(Colors.White, 1), null);

        if (Animations && Theme.IllustrationSpeed > 0.01)
            _spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(140 / Theme.IllustrationSpeed)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    // ---- Grid (Neon City) --------------------------------------------------------------------

    private void BuildGrid(Color a, Color b, Color tx)
    {
        // Sonne mit Scheibenstreifen
        var sun = Circle(300, 250, 150, 150, new LinearGradientBrush(b, a, 90), null);
        var stripes = new DrawingBrush();
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
        for (int i = 0; i < 5; i++)
            dg.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 54 + i * 9, 100, 2.5 + i * 1.6))));
        stripes.Drawing = dg;
        sun.OpacityMask = stripes;

        // Skyline-Silhouette
        double[] heights = { 60, 110, 80, 150, 95, 130, 70, 160, 100, 75, 125, 90 };
        for (int i = 0; i < heights.Length; i++)
        {
            double h = heights[i], x = 10 + i * 48;
            var r = new Rectangle { Width = 40, Height = h, Fill = new SolidColorBrush(Color.FromRgb(10, 5, 28)), Stroke = Alpha(a, 0.55), StrokeThickness = 1.5 };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, 360 - h);
            _canvas.Children.Add(r);
            for (int w = 0; w < 3; w++)
                if ((i + w) % 2 == 0) { var win = new Rectangle { Width = 5, Height = 5, Fill = Alpha(b, 0.85) }; Canvas.SetLeft(win, x + 8 + w * 11); Canvas.SetTop(win, 360 - h + 12 + (i * 7 + w * 13) % (int)(h - 24)); _canvas.Children.Add(win); }
        }

        // Perspektivisches Gitter
        Ln(0, 360, 600, 360, Alpha(a, 0.95), 3);
        for (int i = -9; i <= 9; i++) Ln(300 + i * 18, 360, 300 + i * 110, 600, Alpha(b, 0.75), 1.6);
        for (int k = 1; k <= 8; k++) { double y = 360 + Math.Pow(k / 8.0, 2) * 240; Ln(0, y, 600, y, Alpha(a, 0.65), 1.6); }
    }

    // ---- Nature ------------------------------------------------------------------------------

    private void BuildNature(Color a, Color b, Color tx)
    {
        Circle(300, 210, 200, 200, Radial(a, 0.22), null);
        Circle(300, 200, 82, 82, new LinearGradientBrush(Color.FromArgb(255, a.R, a.G, a.B), Color.FromArgb(160, b.R, b.G, b.B), 90), null);
        // Hügel
        Geo("M0,420 C90,330 190,330 300,400 C400,455 500,350 600,380 L600,600 L0,600 Z", Alpha(b, 0.35), Alpha(b, 0.7), 2);
        Geo("M0,470 C120,390 230,420 330,455 C430,490 520,420 600,440 L600,600 L0,600 Z", Alpha(a, 0.40), Alpha(a, 0.8), 2);
        Geo("M0,530 C140,480 250,520 360,520 C460,520 540,490 600,505 L600,600 L0,600 Z", Alpha(a, 0.62), Alpha(tx, 0.5), 2);
        // Blätter
        string leaf = "M0,0 C30,-46 90,-46 120,0 C90,46 30,46 0,0 Z";
        void Leaf(double x, double y, double angle, double scale, Brush fill)
        {
            var p = Geo(leaf, fill, Alpha(tx, 0.55), 1.5);
            p.RenderTransform = new TransformGroup { Children = { new ScaleTransform(scale, scale), new RotateTransform(angle), new TranslateTransform(x, y) } };
            p.RenderTransformOrigin = new Point(0, 0);
        }
        Leaf(70, 130, -55, 1.0, Alpha(a, 0.5));
        Leaf(500, 90, 215, 0.85, Alpha(b, 0.45));
        Leaf(110, 300, -20, 0.7, Alpha(a, 0.35));
        Leaf(540, 270, 160, 0.75, Alpha(a, 0.4));
    }

    // ---- Corporate ---------------------------------------------------------------------------

    private void BuildCorporate(Color a, Color b, Color tx)
    {
        Circle(300, 300, 270, 270, Radial(a, 0.16), null);
        var sq = new Rectangle { Width = 360, Height = 360, RadiusX = 36, RadiusY = 36, Stroke = Alpha(b, 0.7), StrokeThickness = 3, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(12) };
        Canvas.SetLeft(sq, 120); Canvas.SetTop(sq, 120);
        _canvas.Children.Add(sq);
        var sq2 = new Rectangle { Width = 300, Height = 300, RadiusX = 30, RadiusY = 30, Fill = Alpha(a, 0.16), Stroke = Alpha(a, 0.9), StrokeThickness = 3, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(-8) };
        Canvas.SetLeft(sq2, 150); Canvas.SetTop(sq2, 150);
        _canvas.Children.Add(sq2);
        Circle(300, 300, 95, 95, new LinearGradientBrush(a, Color.FromArgb(120, a.R, a.G, a.B), 60), Alpha(tx, 0.7), 2);
        Circle(300, 300, 40, 40, Alpha(Colors.White, 0.95), null);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                Circle(60 + c * 26, 470 + r * 26, 5, 5, Alpha(b, 0.8), null);
        for (int i = 0; i < 4; i++)
        {
            var bar = new Rectangle { Width = 22, Height = 40 + i * 28, Fill = Alpha(a, 0.5 + i * 0.12), RadiusX = 4, RadiusY = 4 };
            Canvas.SetLeft(bar, 440 + i * 34); Canvas.SetTop(bar, 540 - (40 + i * 28));
            _canvas.Children.Add(bar);
        }
    }
}
