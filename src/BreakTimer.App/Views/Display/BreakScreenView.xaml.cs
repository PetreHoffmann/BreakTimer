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
using BreakTimer.App.Converters;
using BreakTimer.App.Views.Controls;

namespace BreakTimer.App.Views.Display;

/// <summary>
/// Das eigentliche Break-Display-Layout. Wird identisch im Vollbild-Anzeigefenster und in der Live-Vorschau
/// verwendet (gleiche DataContext-Instanz = gleiche Zeit, gleiches Design).
/// </summary>
public partial class BreakScreenView : DisplayControlBase
{
    private string _lastCountdown;
    private bool _finishedAnimationActive;

    public BreakScreenView()
    {
        InitializeComponent();
    }

    protected override void OnAttached()
    {
        _lastCountdown = Vm?.CountdownText;
        // "Countdown Fade-In" beim Erscheinen
        if (Animations && Theme.TransitionEffects)
        {
            MainPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1100)) { BeginTime = TimeSpan.FromMilliseconds(250), EasingFunction = new QuadraticEase() });
            var slide = new TranslateTransform();
            MainPanel.RenderTransform = slide;
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(1100)) { BeginTime = TimeSpan.FromMilliseconds(250), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            MainPanel.Opacity = 0;
        }
        else MainPanel.Opacity = 1;
    }

    protected override void OnDetached()
    {
        MainPanel.BeginAnimation(OpacityProperty, null);
        Countdown.BeginAnimation(OpacityProperty, null);
        _finishedAnimationActive = false;
    }

    protected override void Refresh(string p)
    {
        if (Theme == null) return;

        if (p == null || p == nameof(Theme.GlowEnabled) || p == nameof(Theme.GlowIntensity) || p == nameof(Theme.GlowColor))
            ApplyGlow();

        if (p == null || p == nameof(Theme.LogoPath) || p == nameof(Theme.LogoVisible) || p == nameof(Theme.LogoSize) || p == nameof(Theme.LogoPosition)
            || p == nameof(Theme.AccentColor) || p == nameof(Theme.SecondaryColor))
            ApplyLogo();

        if (p == "vm:CountdownText") PulseCountdown();
        if (p == null || p == "vm:IsFinished" || p == "vm:AnimationsEnabled") ApplyFinishedState();
    }

    private void ApplyGlow()
    {
        if (!Theme.GlowEnabled || Theme.GlowIntensity <= 0)
        {
            Countdown.Effect = null;
            Heading.Effect = null;
            return;
        }
        Color glow = Col(Theme.GlowColor, Colors.Cyan);
        double k = Theme.GlowIntensity / 100.0;
        Countdown.Effect = new DropShadowEffect { Color = glow, BlurRadius = 8 + 70 * k, ShadowDepth = 0, Opacity = 0.35 + 0.6 * k };
        Heading.Effect = new DropShadowEffect { Color = glow, BlurRadius = 4 + 26 * k, ShadowDepth = 0, Opacity = 0.2 + 0.5 * k };
    }

    private void ApplyLogo()
    {
        var t = Theme;
        if (!t.LogoVisible)
        {
            LogoHost.Content = null;
            LogoHost.Visibility = Visibility.Collapsed;
            return;
        }
        LogoHost.Visibility = Visibility.Visible;
        ImageSource src = PathToImageConverter.Load(t.LogoPath, 1024);
        if (src != null)
        {
            LogoHost.Content = new Image { Source = src, Height = t.LogoSize, Stretch = Stretch.Uniform, SnapsToDevicePixels = false };
            RenderOptions.SetBitmapScalingMode((DependencyObject)LogoHost.Content, BitmapScalingMode.HighQuality);
        }
        else
        {
            // Kein (oder fehlendes) Logo: eigenes Break-Timer-Zeichen in den Theme-Akzentfarben
            LogoHost.Content = new BrandMark
            {
                Width = t.LogoSize, Height = t.LogoSize,
                AccentBrush = Solid(t.AccentColor, 255, Colors.Cyan),
                SecondaryBrush = Solid(t.SecondaryColor, 255, Colors.Magenta)
            };
        }
        // Logo-Position relativ zum Text
        switch (t.LogoPosition)
        {
            case "Right": DockPanel.SetDock(LogoHost, Dock.Right); LogoHost.Margin = new Thickness(22, 0, 0, 0); break;
            case "Top": DockPanel.SetDock(LogoHost, Dock.Top); LogoHost.Margin = new Thickness(0, 0, 0, 12); LogoHost.HorizontalAlignment = HorizontalAlignment.Left; break;
            default: DockPanel.SetDock(LogoHost, Dock.Left); LogoHost.Margin = new Thickness(0, 0, 22, 0); break;
        }
    }

    /// <summary>Dezente Einblendung bei Zeitänderung (nur wenn Countdown-Animation aktiv).</summary>
    private void PulseCountdown()
    {
        string now = Vm?.CountdownText;
        if (now == _lastCountdown) return;
        _lastCountdown = now;
        if (!Animations || !Theme.CountdownAnimation || _finishedAnimationActive) return;
        Countdown.BeginAnimation(OpacityProperty, new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    /// <summary>Animation bei 00:00: sanftes Pulsieren des Countdowns und kurzes "Aufpoppen" der Überschrift.</summary>
    private void ApplyFinishedState()
    {
        bool finished = Vm != null && Vm.IsFinished;
        if (finished && Animations)
        {
            _finishedAnimationActive = true;
            Countdown.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(900))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            var pop = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
            HeadingScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            HeadingScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
        else if (_finishedAnimationActive)
        {
            _finishedAnimationActive = false;
            Countdown.BeginAnimation(OpacityProperty, null);
            Countdown.Opacity = 1;
        }
    }
}
