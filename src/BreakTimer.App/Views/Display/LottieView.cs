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
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BreakTimer.Core.Models;
using BreakTimer.Core.Util;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace BreakTimer.App.Views.Display;

/// <summary>
/// Spielt eine Lottie-Animation (Skottie/SkiaSharp) auf der Display-Zeichenfläche ab.
/// Es gibt zwei Instanzen ("Behind" und "Front"); nur die zur Theme-Einstellung passende ist aktiv.
/// Ohne Animationen (Hauptschalter) wird ein ruhiges Standbild gezeigt.
/// </summary>
public sealed class LottieView : DisplayControlBase
{
    private const int MaxPixelWidth = 1920;
    private const int MinPixelWidth = 480;
    private const double PosterProgress = 0.35;

    private readonly Image _image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false, SnapsToDevicePixels = false };
    private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
    private readonly TranslateTransform _move = new TranslateTransform();
    private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _clock = new Stopwatch();

    private Animation _animation;
    private string _loadedPath;
    private SKBitmap _bitmap;
    private WriteableBitmap _wb;
    private bool _failed;

    public LottieView()
    {
        IsHitTestVisible = false;
        Focusable = false;
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_move);
        _image.RenderTransform = group;
        _image.RenderTransformOrigin = new Point(0.5, 0.5);
        Content = _image;
        Visibility = Visibility.Collapsed;
        _timer.Tick += (s, e) => RenderFrame();
    }

    /// <summary>"Behind" oder "Front": auf welcher Ebene diese Instanz erscheint.</summary>
    public string Layer { get; set; } = "Behind";

    protected override void OnDetached()
    {
        _timer.Stop();
        _clock.Stop();
        ReleaseAnimation();
    }

    protected override void Refresh(string p)
    {
        Theme t = Theme;
        if (t == null) return;

        bool wanted = !string.IsNullOrWhiteSpace(t.LottiePath) && File.Exists(t.LottiePath)
                      && string.Equals(t.LottieLayer, Layer, StringComparison.OrdinalIgnoreCase);
        if (!wanted)
        {
            Visibility = Visibility.Collapsed;
            _timer.Stop(); _clock.Stop();
            ReleaseAnimation();
            return;
        }

        if (!string.Equals(_loadedPath, t.LottiePath, StringComparison.OrdinalIgnoreCase) || (_animation == null && !_failed))
            LoadAnimation(t.LottiePath);
        if (_animation == null) { Visibility = Visibility.Collapsed; return; }

        Visibility = Visibility.Visible;
        Opacity = Clamp(t.LottieOpacity, 0, 1);
        _scale.ScaleX = _scale.ScaleY = Math.Max(0.05, t.LottieScale);
        _move.X = t.LottieOffsetX;
        _move.Y = t.LottieOffsetY;

        if (Animations)
        {
            if (!_timer.IsEnabled) { _timer.Start(); if (!_clock.IsRunning) _clock.Start(); }
        }
        else
        {
            _timer.Stop(); _clock.Stop();
            RenderFrame(poster: true);
        }
    }

    private void LoadAnimation(string path)
    {
        ReleaseAnimation();
        _loadedPath = path;
        _failed = false;
        try
        {
            string json = File.ReadAllText(path);
            if (!Animation.TryParse(json, out Animation anim) || anim == null || anim.Duration <= TimeSpan.Zero)
                throw new InvalidDataException("Die Lottie-Datei konnte von Skottie nicht gelesen werden.");
            _animation = anim;
            _clock.Restart();
        }
        catch (Exception ex)
        {
            _failed = true;
            _animation = null;
            Logger.Error("Lottie-Animation konnte nicht geladen werden: " + path, ex);
        }
    }

    private void ReleaseAnimation()
    {
        try { _animation?.Dispose(); } catch { }
        try { _bitmap?.Dispose(); } catch { }
        _animation = null; _bitmap = null; _wb = null;
        _image.Source = null;
        _loadedPath = null;
        _failed = false;
    }

    private void RenderFrame(bool poster = false)
    {
        Animation anim = _animation;
        Theme t = Theme;
        if (anim == null || t == null) return;
        try
        {
            double duration = anim.Duration.TotalSeconds;
            double time;
            if (poster) time = duration * PosterProgress;
            else
            {
                double elapsed = _clock.Elapsed.TotalSeconds * Math.Max(0.1, t.LottieSpeed);
                time = t.LottieLoop ? elapsed % duration : Math.Min(elapsed, duration - 0.0001);
            }

            SKSize size = anim.Size;
            if (size.Width <= 0 || size.Height <= 0) return;

            // Fläche entspricht dem 1920x1080-Zeichenbereich; Pixelgröße richtet sich nach der tatsächlichen Darstellungsgröße.
            int pw = ChoosePixelWidth();
            int ph = Math.Max(1, (int)Math.Round(pw * 9.0 / 16.0));
            EnsureBitmap(pw, ph);

            using (var canvas = new SKCanvas(_bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                SKRect dest = FitRect(size, pw, ph, string.Equals(t.LottieFit, "UniformToFill", StringComparison.OrdinalIgnoreCase));
                canvas.Save();
                if (dest.Width > pw || dest.Height > ph) canvas.ClipRect(new SKRect(0, 0, pw, ph));
                anim.SeekFrameTime(time);
                anim.Render(canvas, dest);
                canvas.Restore();
            }

            _wb.Lock();
            try
            {
                _wb.WritePixels(new Int32Rect(0, 0, pw, ph), _bitmap.GetPixels(), _bitmap.RowBytes * ph, _bitmap.RowBytes);
            }
            finally { _wb.Unlock(); }
        }
        catch (Exception ex)
        {
            _timer.Stop();
            Logger.Error("Lottie-Wiedergabe fehlgeschlagen", ex);
        }
    }

    private static SKRect FitRect(SKSize anim, int w, int h, bool cover)
    {
        double sx = w / (double)anim.Width, sy = h / (double)anim.Height;
        double s = cover ? Math.Max(sx, sy) : Math.Min(sx, sy);
        float rw = (float)(anim.Width * s), rh = (float)(anim.Height * s);
        float x = (w - rw) / 2f, y = (h - rh) / 2f;
        return new SKRect(x, y, x + rw, y + rh);
    }

    private int ChoosePixelWidth()
    {
        double w = 1280;
        try
        {
            // Tatsächliche Breite in Geräte-Pixeln (Viewbox-Skalierung und DPI eingerechnet).
            var root = Window.GetWindow(this) as Visual;
            PresentationSource src = root != null ? PresentationSource.FromVisual(root) : null;
            if (root != null && src?.CompositionTarget != null)
            {
                Point a = TransformToAncestor(root).Transform(new Point(0, 0));
                Point b = TransformToAncestor(root).Transform(new Point(ActualWidth, 0));
                double dip = Math.Abs(b.X - a.X);
                w = dip * src.CompositionTarget.TransformToDevice.M11;
            }
        }
        catch { }
        return (int)Math.Max(MinPixelWidth, Math.Min(MaxPixelWidth, Math.Round(w / 16.0) * 16));
    }

    private void EnsureBitmap(int w, int h)
    {
        if (_bitmap != null && _bitmap.Width == w && _bitmap.Height == h && _wb != null) return;
        _bitmap?.Dispose();
        _bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
        _image.Source = _wb;
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
}
