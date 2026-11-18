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
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Models;

/// <summary>
/// Vollständige Gestaltung des Break Displays. Alle Eigenschaften lösen PropertyChanged aus,
/// damit Break Display und Live-Vorschau sofort reagieren. Farben sind Hex-Werte (#RRGGBB oder #AARRGGBB).
/// </summary>
public sealed class Theme : ObservableObject
{
    // ---- Identität -------------------------------------------------------------------------
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "Neues Theme";
    private bool _isBuiltIn;
    public string Id { get => _id; set => Set(ref _id, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public bool IsBuiltIn { get => _isBuiltIn; set => Set(ref _isBuiltIn, value); }

    // ---- Farben ----------------------------------------------------------------------------
    private string _backgroundColor = "#0B0716";
    private string _gradientStart = "#1B0F3D";
    private string _gradientEnd = "#05020F";
    private bool _gradientEnabled = true;
    private double _gradientAngle = 135;
    private string _countdownColor = "#FFFFFF";
    private string _headingColor = "#FFFFFF";
    private string _textColor = "#D9E4FF";
    private string _accentColor = "#18E6FF";
    private string _secondaryColor = "#FF2FB9";
    private string _glowColor = "#18E6FF";
    private string _footerColor = "#9AA4C7";
    public string BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, value); }
    public string GradientStart { get => _gradientStart; set => Set(ref _gradientStart, value); }
    public string GradientEnd { get => _gradientEnd; set => Set(ref _gradientEnd, value); }
    public bool GradientEnabled { get => _gradientEnabled; set => Set(ref _gradientEnabled, value); }
    public double GradientAngle { get => _gradientAngle; set => Set(ref _gradientAngle, value); }
    public string CountdownColor { get => _countdownColor; set => Set(ref _countdownColor, value); }
    public string HeadingColor { get => _headingColor; set => Set(ref _headingColor, value); }
    public string TextColor { get => _textColor; set => Set(ref _textColor, value); }
    public string AccentColor { get => _accentColor; set => Set(ref _accentColor, value); }
    public string SecondaryColor { get => _secondaryColor; set => Set(ref _secondaryColor, value); }
    public string GlowColor { get => _glowColor; set => Set(ref _glowColor, value); }
    public string FooterColor { get => _footerColor; set => Set(ref _footerColor, value); }

    // ---- Texte -----------------------------------------------------------------------------
    private string _companyName = "Break Timer";
    private string _slogan = "Pause. Durchatmen. Weiter.";
    private string _heading = "Kurze Pause";
    private string _breakText = "Bitte entspannen Sie sich einen Moment";
    private string _endTimeText = "Wir machen weiter um {time} Uhr";
    private string _footerText = "";
    private string _website = "www.breaktimer.app";
    private string _finishedText = "Pause beendet!";
    public string CompanyName { get => _companyName; set => Set(ref _companyName, value); }
    public string Slogan { get => _slogan; set => Set(ref _slogan, value); }
    public string Heading { get => _heading; set => Set(ref _heading, value); }
    public string BreakText { get => _breakText; set => Set(ref _breakText, value); }
    /// <summary>Platzhalter {time} wird durch die voraussichtliche Endzeit ersetzt.</summary>
    public string EndTimeText { get => _endTimeText; set => Set(ref _endTimeText, value); }
    public string FooterText { get => _footerText; set => Set(ref _footerText, value); }
    public string Website { get => _website; set => Set(ref _website, value); }
    public string FinishedText { get => _finishedText; set => Set(ref _finishedText, value); }

    // ---- Schrift ---------------------------------------------------------------------------
    private string _headingFont = "Segoe UI";
    private string _bodyFont = "Segoe UI";
    private string _countdownFont = "Bahnschrift, Segoe UI";
    private double _headingSize = 112;
    private double _bodySize = 46;
    private double _countdownSize = 380;
    private string _headingWeight = "SemiBold";
    private string _bodyWeight = "Normal";
    private string _countdownWeight = "Light";
    private string _textAlignment = "Center";
    private double _lineSpacing = 1.15;
    public string HeadingFont { get => _headingFont; set => Set(ref _headingFont, value); }
    public string BodyFont { get => _bodyFont; set => Set(ref _bodyFont, value); }
    public string CountdownFont { get => _countdownFont; set => Set(ref _countdownFont, value); }
    public double HeadingSize { get => _headingSize; set => Set(ref _headingSize, value); }
    public double BodySize { get => _bodySize; set => Set(ref _bodySize, value); }
    public double CountdownSize { get => _countdownSize; set => Set(ref _countdownSize, value); }
    public string HeadingWeight { get => _headingWeight; set => Set(ref _headingWeight, value); }
    public string BodyWeight { get => _bodyWeight; set => Set(ref _bodyWeight, value); }
    public string CountdownWeight { get => _countdownWeight; set => Set(ref _countdownWeight, value); }
    /// <summary>Left, Center oder Right.</summary>
    public string TextAlignment { get => _textAlignment; set => Set(ref _textAlignment, value); }
    public double LineSpacing { get => _lineSpacing; set => Set(ref _lineSpacing, value); }

    // ---- Layout ----------------------------------------------------------------------------
    private string _headerPosition = "Left";
    private string _footerPosition = "Center";
    private string _illustrationPosition = "Right";
    private double _countdownOffsetX;
    private double _countdownOffsetY;
    private double _illustrationOffsetX;
    private double _illustrationOffsetY;
    private double _illustrationScale = 1.0;
    private double _illustrationOpacity = 0.9;
    private double _illustrationSpeed = 1.0;
    private string _lottiePath = "";
    private string _lottieLayer = "Behind";
    private string _lottieFit = "Uniform";
    private double _lottieScale = 1.0;
    private double _lottieOpacity = 1.0;
    private double _lottieOffsetX;
    private double _lottieOffsetY;
    private double _lottieSpeed = 1.0;
    private bool _lottieLoop = true;
    private double _horizontalPadding = 90;
    private double _verticalPadding = 60;
    private double _mainScale = 1.0;
    private double _headerScale = 1.0;
    private double _footerScale = 1.0;
    private bool _showHeader = true;
    private bool _showFooter = true;
    private bool _showIllustration = true;
    private string _illustrationStyle = "Orbit";
    public string HeaderPosition { get => _headerPosition; set => Set(ref _headerPosition, value); }
    public string FooterPosition { get => _footerPosition; set => Set(ref _footerPosition, value); }
    public string IllustrationPosition { get => _illustrationPosition; set => Set(ref _illustrationPosition, value); }
    public double CountdownOffsetX { get => _countdownOffsetX; set => Set(ref _countdownOffsetX, value); }
    public double CountdownOffsetY { get => _countdownOffsetY; set => Set(ref _countdownOffsetY, value); }
    public double IllustrationOffsetX { get => _illustrationOffsetX; set => Set(ref _illustrationOffsetX, value); }
    public double IllustrationOffsetY { get => _illustrationOffsetY; set => Set(ref _illustrationOffsetY, value); }
    public double IllustrationScale { get => _illustrationScale; set => Set(ref _illustrationScale, value); }
    /// <summary>Drehgeschwindigkeit der Grafik (1 = Standard, 0 = steht still).</summary>
    public double IllustrationSpeed { get => _illustrationSpeed; set => Set(ref _illustrationSpeed, value); }
    /// <summary>Pfad zu einer Lottie-Animation (.json) im Ressourcenordner; leer = keine Lottie-Animation.</summary>
    public string LottiePath { get => _lottiePath; set => Set(ref _lottiePath, value); }
    /// <summary>"Behind" = hinter Texten und Countdown, "Front" = vor allem.</summary>
    public string LottieLayer { get => _lottieLayer; set => Set(ref _lottieLayer, value); }
    /// <summary>"Uniform" (ganz sichtbar) oder "UniformToFill" (Fläche füllen).</summary>
    public string LottieFit { get => _lottieFit; set => Set(ref _lottieFit, value); }
    public double LottieScale { get => _lottieScale; set => Set(ref _lottieScale, value); }
    public double LottieOpacity { get => _lottieOpacity; set => Set(ref _lottieOpacity, value); }
    public double LottieOffsetX { get => _lottieOffsetX; set => Set(ref _lottieOffsetX, value); }
    public double LottieOffsetY { get => _lottieOffsetY; set => Set(ref _lottieOffsetY, value); }
    public double LottieSpeed { get => _lottieSpeed; set => Set(ref _lottieSpeed, value); }
    public bool LottieLoop { get => _lottieLoop; set => Set(ref _lottieLoop, value); }
    public double IllustrationOpacity { get => _illustrationOpacity; set => Set(ref _illustrationOpacity, value); }
    public double HorizontalPadding { get => _horizontalPadding; set => Set(ref _horizontalPadding, value); }
    public double VerticalPadding { get => _verticalPadding; set => Set(ref _verticalPadding, value); }
    public double MainScale { get => _mainScale; set => Set(ref _mainScale, value); }
    public double HeaderScale { get => _headerScale; set => Set(ref _headerScale, value); }
    public double FooterScale { get => _footerScale; set => Set(ref _footerScale, value); }
    public bool ShowHeader { get => _showHeader; set => Set(ref _showHeader, value); }
    public bool ShowFooter { get => _showFooter; set => Set(ref _showFooter, value); }
    public bool ShowIllustration { get => _showIllustration; set => Set(ref _showIllustration, value); }
    /// <summary>Orbit, Grid, Nature, Corporate.</summary>
    public string IllustrationStyle { get => _illustrationStyle; set => Set(ref _illustrationStyle, value); }

    // ---- Hintergrund -----------------------------------------------------------------------
    private string _backgroundImagePath = "";
    private string _backgroundImageStretch = "UniformToFill";
    private double _backgroundImageScale = 100;
    private double _backgroundImageOffsetX;
    private double _backgroundImageOffsetY;
    private bool _overlayEnabled;
    private string _overlayColor = "#000000";
    private double _overlayOpacity = 40;
    public string BackgroundImagePath { get => _backgroundImagePath; set => Set(ref _backgroundImagePath, value); }
    /// <summary>UniformToFill, Uniform, Fill oder None.</summary>
    public string BackgroundImageStretch { get => _backgroundImageStretch; set => Set(ref _backgroundImageStretch, value); }
    /// <summary>Prozent (50-300).</summary>
    public double BackgroundImageScale { get => _backgroundImageScale; set => Set(ref _backgroundImageScale, value); }
    /// <summary>Verschiebung in Prozent der Breite (-50..50).</summary>
    public double BackgroundImageOffsetX { get => _backgroundImageOffsetX; set => Set(ref _backgroundImageOffsetX, value); }
    public double BackgroundImageOffsetY { get => _backgroundImageOffsetY; set => Set(ref _backgroundImageOffsetY, value); }
    public bool OverlayEnabled { get => _overlayEnabled; set => Set(ref _overlayEnabled, value); }
    public string OverlayColor { get => _overlayColor; set => Set(ref _overlayColor, value); }
    /// <summary>Deckkraft in Prozent (0-100).</summary>
    public double OverlayOpacity { get => _overlayOpacity; set => Set(ref _overlayOpacity, value); }

    // ---- Logo ------------------------------------------------------------------------------
    private string _logoPath = "";
    private bool _logoVisible = true;
    private double _logoSize = 96;
    private string _logoPosition = "Left";
    public string LogoPath { get => _logoPath; set => Set(ref _logoPath, value); }
    public bool LogoVisible { get => _logoVisible; set => Set(ref _logoVisible, value); }
    public double LogoSize { get => _logoSize; set => Set(ref _logoSize, value); }
    /// <summary>Left, Right oder Top (relativ zum Firmennamen).</summary>
    public string LogoPosition { get => _logoPosition; set => Set(ref _logoPosition, value); }

    // ---- Effekte ---------------------------------------------------------------------------
    private bool _glowEnabled = true;
    private double _glowIntensity = 55;
    private bool _backgroundAnimation = true;
    private bool _particlesEnabled = true;
    private bool _lightAnimation = true;
    private bool _countdownAnimation = true;
    private bool _transitionEffects = true;
    public bool GlowEnabled { get => _glowEnabled; set => Set(ref _glowEnabled, value); }
    /// <summary>0-100.</summary>
    public double GlowIntensity { get => _glowIntensity; set => Set(ref _glowIntensity, value); }
    public bool BackgroundAnimation { get => _backgroundAnimation; set => Set(ref _backgroundAnimation, value); }
    public bool ParticlesEnabled { get => _particlesEnabled; set => Set(ref _particlesEnabled, value); }
    public bool LightAnimation { get => _lightAnimation; set => Set(ref _lightAnimation, value); }
    public bool CountdownAnimation { get => _countdownAnimation; set => Set(ref _countdownAnimation, value); }
    public bool TransitionEffects { get => _transitionEffects; set => Set(ref _transitionEffects, value); }

    // ---- Hilfsfunktionen -------------------------------------------------------------------

    private static readonly PropertyInfo[] DataProperties = typeof(Theme)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
        .ToArray();

    /// <summary>Kopiert alle Werte (inkl. Id/Name) von einem anderen Theme; löst PropertyChanged je Änderung aus.</summary>
    public void CopyFrom(Theme other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        foreach (PropertyInfo p in DataProperties) p.SetValue(this, p.GetValue(other, null), null);
    }

    public Theme Clone()
    {
        var t = new Theme();
        t.CopyFrom(this);
        return t;
    }

    public override string ToString() => Name;
}
