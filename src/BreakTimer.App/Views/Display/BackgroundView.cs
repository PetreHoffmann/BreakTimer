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
using System.Windows.Shapes;
using BreakTimer.App.Converters;

namespace BreakTimer.App.Views.Display;

/// <summary>Hintergrundebene: Farbe, Verlauf, Bild, Overlay, Lichtbewegung und Partikel (alles optional und abschaltbar).</summary>
public sealed class BackgroundView : DisplayControlBase
{
    private readonly Grid _root = new Grid { ClipToBounds = true };
    private readonly Rectangle _base = new Rectangle();
    private readonly Rectangle _gradient = new Rectangle();
    private readonly RotateTransform _gradRotate = new RotateTransform { CenterX = 0.5, CenterY = 0.5 };
    private readonly GradientStop _gs0 = new GradientStop { Offset = 0 };
    private readonly GradientStop _gs1 = new GradientStop { Offset = 1 };
    private readonly Image _image = new Image { RenderTransformOrigin = new Point(0.5, 0.5), Stretch = Stretch.UniformToFill };
    private readonly ScaleTransform _imgScale = new ScaleTransform(1, 1);
    private readonly TranslateTransform _imgMove = new TranslateTransform();
    private readonly Canvas _lightLayer = new Canvas { IsHitTestVisible = false };
    private readonly Ellipse _blobA = new Ellipse(), _blobB = new Ellipse();
    private readonly TranslateTransform _moveA = new TranslateTransform(), _moveB = new TranslateTransform();
    private readonly Rectangle _sweep = new Rectangle { IsHitTestVisible = false };
    private readonly TranslateTransform _sweepMove = new TranslateTransform();
    private readonly Rectangle _overlay = new Rectangle { IsHitTestVisible = false };
    private readonly ParticleField _particles = new ParticleField();
    private string _loadedImagePath;

    public BackgroundView()
    {
        IsHitTestVisible = false;
        Content = _root;
        _gradient.Fill = new LinearGradientBrush(new GradientStopCollection { _gs0, _gs1 }, new Point(0, 0), new Point(1, 1))
        { RelativeTransform = _gradRotate };

        var group = new TransformGroup();
        group.Children.Add(_imgScale);
        group.Children.Add(_imgMove);
        _image.RenderTransform = group;

        _blobA.RenderTransform = _moveA; _blobB.RenderTransform = _moveB;
        _blobA.IsHitTestVisible = false; _blobB.IsHitTestVisible = false;
        _sweep.RenderTransform = new TransformGroup { Children = { new SkewTransform(-18, 0), _sweepMove } };

        _lightLayer.Children.Add(_blobA);
        _lightLayer.Children.Add(_blobB);
        _lightLayer.Children.Add(_sweep);

        _root.Children.Add(_base);
        _root.Children.Add(_gradient);
        _root.Children.Add(_image);
        _root.Children.Add(_lightLayer);
        _root.Children.Add(_overlay);
        _root.Children.Add(_particles);
        SizeChanged += (s, e) => { LayoutLights(); RestartAnimations(); };
    }

    protected override void OnDetached()
    {
        StopAnimations();
        _particles.Stop();
    }

    protected override void Refresh(string p)
    {
        if (Theme == null) return;
        var t = Theme;
        _base.Fill = Solid(t.BackgroundColor, 255, Colors.Black);

        _gradient.Visibility = t.GradientEnabled ? Visibility.Visible : Visibility.Collapsed;
        _gs0.Color = Col(t.GradientStart, Colors.Black);
        _gs1.Color = Col(t.GradientEnd, Colors.Black);
        _gradRotate.Angle = t.GradientAngle - 45;

        if (p == null || p == nameof(t.BackgroundImagePath) || _loadedImagePath != t.BackgroundImagePath)
        {
            _loadedImagePath = t.BackgroundImagePath;
            _image.Source = PathToImageConverter.Load(t.BackgroundImagePath, 3840);
        }
        _image.Visibility = _image.Source == null ? Visibility.Collapsed : Visibility.Visible;
        Stretch st;
        _image.Stretch = Enum.TryParse(t.BackgroundImageStretch, out st) ? st : Stretch.UniformToFill;
        _imgScale.ScaleX = _imgScale.ScaleY = Math.Max(0.1, t.BackgroundImageScale / 100.0);
        UpdateImageOffset();

        _overlay.Visibility = t.OverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
        _overlay.Fill = Solid(t.OverlayColor, 255, Colors.Black);
        _overlay.Opacity = Math.Max(0, Math.Min(1, t.OverlayOpacity / 100.0));

        Color accent = Col(t.AccentColor, Colors.Cyan), second = Col(t.SecondaryColor, Colors.Magenta);
        _blobA.Fill = Radial(accent, 0.34);
        _blobB.Fill = Radial(second, 0.30);
        _sweep.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(26, 255, 255, 255), 0.5),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
        }, 0);
        _particles.SetColor(accent);

        LayoutLights();
        if (p == null || p == "vm:AnimationsEnabled" || p == nameof(t.BackgroundAnimation) || p == nameof(t.LightAnimation) || p == nameof(t.ParticlesEnabled))
            RestartAnimations();
    }

    private static Brush Radial(Color c, double alpha)
    {
        var b = new RadialGradientBrush(Color.FromArgb((byte)(255 * alpha), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    private void UpdateImageOffset()
    {
        _imgMove.X = Theme.BackgroundImageOffsetX / 100.0 * ActualWidth;
        _imgMove.Y = Theme.BackgroundImageOffsetY / 100.0 * ActualHeight;
    }

    private void LayoutLights()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Theme == null) return;
        double d = Math.Max(w, h) * 0.75;
        _blobA.Width = _blobA.Height = d;
        _blobB.Width = _blobB.Height = d * 0.9;
        Canvas.SetLeft(_blobA, -d * 0.2); Canvas.SetTop(_blobA, -d * 0.25);
        Canvas.SetLeft(_blobB, w - d * 0.7); Canvas.SetTop(_blobB, h - d * 0.65);
        _sweep.Width = w * 0.22; _sweep.Height = h * 1.3;
        Canvas.SetTop(_sweep, -h * 0.15);
        UpdateImageOffset();
    }

    // ---- Animationen -------------------------------------------------------------------------

    private void StopAnimations()
    {
        _gradRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        _moveA.BeginAnimation(TranslateTransform.XProperty, null); _moveA.BeginAnimation(TranslateTransform.YProperty, null);
        _moveB.BeginAnimation(TranslateTransform.XProperty, null); _moveB.BeginAnimation(TranslateTransform.YProperty, null);
        _sweepMove.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private void RestartAnimations()
    {
        if (Theme == null) return;
        StopAnimations();
        double w = ActualWidth, h = ActualHeight;
        bool anim = Animations && IsLoaded && w > 0;

        bool bgAnim = anim && Theme.BackgroundAnimation;
        _blobA.Visibility = _blobB.Visibility = Theme.BackgroundAnimation || Theme.LightAnimation ? Visibility.Visible : Visibility.Collapsed;
        if (bgAnim)
        {
            double baseAngle = Theme.GradientAngle - 45;
            _gradRotate.BeginAnimation(RotateTransform.AngleProperty, Sine(baseAngle - 14, baseAngle + 14, 38));
            _moveA.BeginAnimation(TranslateTransform.XProperty, Sine(0, w * 0.18, 31));
            _moveA.BeginAnimation(TranslateTransform.YProperty, Sine(0, h * 0.14, 43));
            _moveB.BeginAnimation(TranslateTransform.XProperty, Sine(0, -w * 0.16, 37));
            _moveB.BeginAnimation(TranslateTransform.YProperty, Sine(0, -h * 0.12, 29));
        }

        _sweep.Visibility = Theme.LightAnimation ? Visibility.Visible : Visibility.Collapsed;
        if (anim && Theme.LightAnimation)
        {
            var sweep = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            sweep.KeyFrames.Add(new LinearDoubleKeyFrame(-w * 0.3, KeyTime.FromPercent(0)));
            sweep.KeyFrames.Add(new LinearDoubleKeyFrame(w * 1.1, KeyTime.FromPercent(0.45)));
            sweep.KeyFrames.Add(new LinearDoubleKeyFrame(w * 1.1, KeyTime.FromPercent(1)));
            sweep.Duration = TimeSpan.FromSeconds(26);
            _sweepMove.BeginAnimation(TranslateTransform.XProperty, sweep);
        }
        else _sweepMove.X = -w;

        if (anim && Theme.ParticlesEnabled) _particles.Start(); else _particles.Stop();
    }

    private static DoubleAnimation Sine(double from, double to, double seconds) => new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
    {
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
    };
}
