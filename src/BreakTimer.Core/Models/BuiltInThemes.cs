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

using System.Collections.Generic;

namespace BreakTimer.Core.Models;

/// <summary>Die fest eingebauten, schreibgeschützten Standard-Themes.</summary>
public static class BuiltInThemes
{
    public const string NeonFutureId = "builtin-neon-future";
    public const string MinimalDarkId = "builtin-minimal-dark";
    public const string NeonCityId = "builtin-neon-city";
    public const string NatureBreakId = "builtin-nature-break";
    public const string CorporateId = "builtin-corporate";

    public static IReadOnlyList<Theme> Create() => new List<Theme>
    {
        NeonFuture(), MinimalDark(), NeonCity(), NatureBreak(), Corporate()
    };

    public static Theme NeonFuture() => new Theme
    {
        Id = NeonFutureId, Name = "Neon Future", IsBuiltIn = true,
        BackgroundColor = "#0B0716", GradientStart = "#2A1260", GradientEnd = "#05020F", GradientAngle = 135,
        AccentColor = "#18E6FF", SecondaryColor = "#FF2FB9", GlowColor = "#18E6FF",
        HeadingColor = "#FFFFFF", TextColor = "#D9E4FF", CountdownColor = "#FFFFFF", FooterColor = "#8F9AC4",
        IllustrationStyle = "Orbit", IllustrationPosition = "Right", IllustrationScale = 0.78, IllustrationOffsetY = -190, IllustrationOffsetX = 20, IllustrationOpacity = 0.8,
        CompanyName = "Break Timer", Slogan = "Pause. Durchatmen. Weiter."
    };

    public static Theme MinimalDark() => new Theme
    {
        Id = MinimalDarkId, Name = "Minimal Dark", IsBuiltIn = true,
        BackgroundColor = "#000000", GradientEnabled = false, GradientStart = "#000000", GradientEnd = "#000000",
        AccentColor = "#FFFFFF", SecondaryColor = "#9A9A9A", GlowColor = "#FFFFFF",
        HeadingColor = "#FFFFFF", TextColor = "#BDBDBD", CountdownColor = "#FFFFFF", FooterColor = "#6E6E6E",
        ShowIllustration = false, IllustrationStyle = "None",
        CountdownSize = 460, CountdownWeight = "Thin", HeadingSize = 96, HeadingWeight = "Light",
        GlowEnabled = false, ParticlesEnabled = false, LightAnimation = false, BackgroundAnimation = false,
        LogoVisible = false, Slogan = "", CompanyName = "", Website = "", BreakText = "",
        HeaderPosition = "Left", FooterPosition = "Center"
    };

    public static Theme NeonCity() => new Theme
    {
        Id = NeonCityId, Name = "Neon City", IsBuiltIn = true,
        BackgroundColor = "#080314", GradientStart = "#3A0B5C", GradientEnd = "#04102C", GradientAngle = 160,
        AccentColor = "#FF3DAE", SecondaryColor = "#2D8CFF", GlowColor = "#FF3DAE",
        HeadingColor = "#FFE6F6", TextColor = "#BFD6FF", CountdownColor = "#FFFFFF", FooterColor = "#8FA8E0",
        IllustrationStyle = "Grid", IllustrationPosition = "Center", IllustrationOpacity = 0.55, IllustrationScale = 1.9,
        IllustrationOffsetY = 190,
        GlowIntensity = 75, HeadingWeight = "Bold", CountdownWeight = "SemiLight",
        Slogan = "Back in a moment."
    };

    public static Theme NatureBreak() => new Theme
    {
        Id = NatureBreakId, Name = "Nature Break", IsBuiltIn = true,
        BackgroundColor = "#082421", GradientStart = "#14574A", GradientEnd = "#0A2A3F", GradientAngle = 120,
        AccentColor = "#8FF0B8", SecondaryColor = "#5CC8F0", GlowColor = "#8FF0B8",
        HeadingColor = "#F2FFF7", TextColor = "#D3F0E0", CountdownColor = "#F2FFF7", FooterColor = "#9CC9B6",
        IllustrationStyle = "Nature", IllustrationPosition = "Right", IllustrationOpacity = 0.85,
        GlowIntensity = 30, ParticlesEnabled = true, LightAnimation = true,
        Heading = "Zeit zum Durchatmen", BreakText = "Gönnen Sie sich einen kurzen Moment der Ruhe",
        Slogan = "Ruhe finden."
    };

    public static Theme Corporate() => new Theme
    {
        Id = CorporateId, Name = "Corporate", IsBuiltIn = true,
        BackgroundColor = "#0F1623", GradientStart = "#1B2A44", GradientEnd = "#0B111C", GradientAngle = 145,
        AccentColor = "#4C8DF6", SecondaryColor = "#9FB3D1", GlowColor = "#4C8DF6",
        HeadingColor = "#FFFFFF", TextColor = "#C5D0E3", CountdownColor = "#FFFFFF", FooterColor = "#8A99B5",
        IllustrationStyle = "Corporate", IllustrationPosition = "Right", IllustrationOpacity = 0.8,
        GlowEnabled = false, ParticlesEnabled = false, LightAnimation = false, BackgroundAnimation = false,
        HeadingWeight = "SemiBold", CountdownWeight = "SemiLight", CountdownSize = 360,
        CompanyName = "Ihr Unternehmen", Slogan = "Gemeinsam weiterkommen", Website = "www.ihr-unternehmen.de",
        Heading = "Pause", BreakText = "Wir setzen die Schulung in Kürze fort"
    };
}
