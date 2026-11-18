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
using System.Text.RegularExpressions;
using BreakTimer.Core.Models;

namespace BreakTimer.Core.Services;

/// <summary>Bereinigt ungültige Werte in Themes (z. B. aus manuell bearbeiteten oder importierten Dateien).</summary>
public static class ThemeValidator
{
    private static readonly Regex HexColor = new Regex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);
    private static readonly string[] Alignments = { "Left", "Center", "Right" };
    private static readonly string[] LogoPositions = { "Left", "Right", "Top" };
    private static readonly string[] Stretches = { "UniformToFill", "Uniform", "Fill", "None" };
    private static readonly string[] Styles = { "Orbit", "Grid", "Nature", "Corporate", "None" };
    private static readonly string[] Weights = { "Thin", "ExtraLight", "Light", "SemiLight", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" };

    public static bool IsValidColor(string value) => !string.IsNullOrEmpty(value) && HexColor.IsMatch(value);

    /// <summary>Korrigiert das Theme in place und liefert die Liste der vorgenommenen Korrekturen.</summary>
    public static IList<string> Sanitize(Theme t)
    {
        var fixes = new List<string>();
        var defaults = new Theme();

        if (string.IsNullOrWhiteSpace(t.Id)) { t.Id = Guid.NewGuid().ToString("N"); fixes.Add("Id"); }
        if (string.IsNullOrWhiteSpace(t.Name)) { t.Name = "Importiertes Theme"; fixes.Add("Name"); }

        Color(t.BackgroundColor, v => t.BackgroundColor = v, defaults.BackgroundColor, "BackgroundColor", fixes);
        Color(t.GradientStart, v => t.GradientStart = v, defaults.GradientStart, "GradientStart", fixes);
        Color(t.GradientEnd, v => t.GradientEnd = v, defaults.GradientEnd, "GradientEnd", fixes);
        Color(t.CountdownColor, v => t.CountdownColor = v, defaults.CountdownColor, "CountdownColor", fixes);
        Color(t.HeadingColor, v => t.HeadingColor = v, defaults.HeadingColor, "HeadingColor", fixes);
        Color(t.TextColor, v => t.TextColor = v, defaults.TextColor, "TextColor", fixes);
        Color(t.AccentColor, v => t.AccentColor = v, defaults.AccentColor, "AccentColor", fixes);
        Color(t.SecondaryColor, v => t.SecondaryColor = v, defaults.SecondaryColor, "SecondaryColor", fixes);
        Color(t.GlowColor, v => t.GlowColor = v, defaults.GlowColor, "GlowColor", fixes);
        Color(t.FooterColor, v => t.FooterColor = v, defaults.FooterColor, "FooterColor", fixes);
        Color(t.OverlayColor, v => t.OverlayColor = v, defaults.OverlayColor, "OverlayColor", fixes);

        Choice(t.TextAlignment, Alignments, v => t.TextAlignment = v, "Center", "TextAlignment", fixes);
        Choice(t.HeaderPosition, Alignments, v => t.HeaderPosition = v, "Left", "HeaderPosition", fixes);
        Choice(t.FooterPosition, Alignments, v => t.FooterPosition = v, "Center", "FooterPosition", fixes);
        Choice(t.IllustrationPosition, Alignments, v => t.IllustrationPosition = v, "Right", "IllustrationPosition", fixes);
        Choice(t.LogoPosition, LogoPositions, v => t.LogoPosition = v, "Left", "LogoPosition", fixes);
        Choice(t.BackgroundImageStretch, Stretches, v => t.BackgroundImageStretch = v, "UniformToFill", "BackgroundImageStretch", fixes);
        Choice(t.IllustrationStyle, Styles, v => t.IllustrationStyle = v, "Orbit", "IllustrationStyle", fixes);
        Choice(t.HeadingWeight, Weights, v => t.HeadingWeight = v, "SemiBold", "HeadingWeight", fixes);
        Choice(t.BodyWeight, Weights, v => t.BodyWeight = v, "Normal", "BodyWeight", fixes);
        Choice(t.CountdownWeight, Weights, v => t.CountdownWeight = v, "Light", "CountdownWeight", fixes);

        Range(t.GradientAngle, 0, 360, v => t.GradientAngle = v, defaults.GradientAngle, "GradientAngle", fixes);
        Range(t.LineSpacing, 0.8, 2.5, v => t.LineSpacing = v, defaults.LineSpacing, "LineSpacing", fixes);
        Range(t.HeadingSize, 24, 300, v => t.HeadingSize = v, defaults.HeadingSize, "HeadingSize", fixes);
        Range(t.BodySize, 16, 140, v => t.BodySize = v, defaults.BodySize, "BodySize", fixes);
        Range(t.CountdownSize, 80, 700, v => t.CountdownSize = v, defaults.CountdownSize, "CountdownSize", fixes);
        Range(t.CountdownOffsetX, -900, 900, v => t.CountdownOffsetX = v, 0, "CountdownOffsetX", fixes);
        Range(t.CountdownOffsetY, -500, 500, v => t.CountdownOffsetY = v, 0, "CountdownOffsetY", fixes);
        Range(t.IllustrationOffsetX, -900, 900, v => t.IllustrationOffsetX = v, 0, "IllustrationOffsetX", fixes);
        Range(t.IllustrationOffsetY, -600, 600, v => t.IllustrationOffsetY = v, 0, "IllustrationOffsetY", fixes);
        Range(t.IllustrationScale, 0.3, 3, v => t.IllustrationScale = v, 1, "IllustrationScale", fixes);
        Choice(t.LottieLayer, new[] { "Behind", "Front" }, v => t.LottieLayer = v, "Behind", "LottieLayer", fixes);
        Choice(t.LottieFit, new[] { "Uniform", "UniformToFill" }, v => t.LottieFit = v, "Uniform", "LottieFit", fixes);
        Range(t.LottieScale, 0.2, 3, v => t.LottieScale = v, 1, "LottieScale", fixes);
        Range(t.LottieOpacity, 0, 1, v => t.LottieOpacity = v, 1, "LottieOpacity", fixes);
        Range(t.LottieOffsetX, -960, 960, v => t.LottieOffsetX = v, 0, "LottieOffsetX", fixes);
        Range(t.LottieOffsetY, -540, 540, v => t.LottieOffsetY = v, 0, "LottieOffsetY", fixes);
        Range(t.LottieSpeed, 0.1, 5, v => t.LottieSpeed = v, 1, "LottieSpeed", fixes);
        Range(t.IllustrationSpeed, 0, 10, v => t.IllustrationSpeed = v, 1, "IllustrationSpeed", fixes);
        Range(t.IllustrationOpacity, 0, 1, v => t.IllustrationOpacity = v, 0.9, "IllustrationOpacity", fixes);
        Range(t.HorizontalPadding, 0, 400, v => t.HorizontalPadding = v, defaults.HorizontalPadding, "HorizontalPadding", fixes);
        Range(t.VerticalPadding, 0, 300, v => t.VerticalPadding = v, defaults.VerticalPadding, "VerticalPadding", fixes);
        Range(t.MainScale, 0.4, 1.6, v => t.MainScale = v, 1, "MainScale", fixes);
        Range(t.HeaderScale, 0.4, 2.0, v => t.HeaderScale = v, 1, "HeaderScale", fixes);
        Range(t.FooterScale, 0.4, 2.0, v => t.FooterScale = v, 1, "FooterScale", fixes);
        Range(t.BackgroundImageScale, 50, 300, v => t.BackgroundImageScale = v, 100, "BackgroundImageScale", fixes);
        Range(t.BackgroundImageOffsetX, -50, 50, v => t.BackgroundImageOffsetX = v, 0, "BackgroundImageOffsetX", fixes);
        Range(t.BackgroundImageOffsetY, -50, 50, v => t.BackgroundImageOffsetY = v, 0, "BackgroundImageOffsetY", fixes);
        Range(t.OverlayOpacity, 0, 100, v => t.OverlayOpacity = v, 40, "OverlayOpacity", fixes);
        Range(t.LogoSize, 24, 400, v => t.LogoSize = v, defaults.LogoSize, "LogoSize", fixes);
        Range(t.GlowIntensity, 0, 100, v => t.GlowIntensity = v, defaults.GlowIntensity, "GlowIntensity", fixes);

        if (string.IsNullOrWhiteSpace(t.HeadingFont)) { t.HeadingFont = defaults.HeadingFont; fixes.Add("HeadingFont"); }
        if (string.IsNullOrWhiteSpace(t.BodyFont)) { t.BodyFont = defaults.BodyFont; fixes.Add("BodyFont"); }
        if (string.IsNullOrWhiteSpace(t.CountdownFont)) { t.CountdownFont = defaults.CountdownFont; fixes.Add("CountdownFont"); }

        // Null-Texte vermeiden
        t.CompanyName = t.CompanyName ?? ""; t.Slogan = t.Slogan ?? ""; t.Heading = t.Heading ?? "";
        t.BreakText = t.BreakText ?? ""; t.EndTimeText = t.EndTimeText ?? ""; t.FooterText = t.FooterText ?? "";
        t.Website = t.Website ?? ""; t.FinishedText = t.FinishedText ?? "";
        t.BackgroundImagePath = t.BackgroundImagePath ?? ""; t.LogoPath = t.LogoPath ?? ""; t.LottiePath = t.LottiePath ?? "";
        return fixes;
    }

    private static void Color(string value, Action<string> set, string fallback, string name, IList<string> fixes)
    {
        if (!IsValidColor(value)) { set(fallback); fixes.Add(name); }
    }

    private static void Choice(string value, string[] allowed, Action<string> set, string fallback, string name, IList<string> fixes)
    {
        string match = allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
        if (match == null) { set(fallback); fixes.Add(name); }
        else if (match != value) set(match);
    }

    private static void Range(double value, double min, double max, Action<double> set, double fallback, string name, IList<string> fixes)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) { set(fallback); fixes.Add(name); }
        else if (value < min) { set(min); fixes.Add(name); }
        else if (value > max) { set(max); fixes.Add(name); }
    }
}
