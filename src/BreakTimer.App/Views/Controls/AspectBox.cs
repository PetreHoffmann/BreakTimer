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

namespace BreakTimer.App.Views.Controls;

/// <summary>Zeigt den Inhalt im festen Seitenverhältnis (Standard 16:9), so groß wie möglich und zentriert.</summary>
public sealed class AspectBox : Decorator
{
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio), typeof(double), typeof(AspectBox), new FrameworkPropertyMetadata(16.0 / 9.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Ratio { get => (double)GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    private Size Fit(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 960 : available.Width;
        double h = double.IsInfinity(available.Height) ? w / Ratio : available.Height;
        double fitW = Math.Min(w, h * Ratio);
        return new Size(Math.Max(0, fitW), Math.Max(0, fitW / Ratio));
    }

    protected override Size MeasureOverride(Size constraint)
    {
        Size s = Fit(constraint);
        Child?.Measure(s);
        return s;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size s = Fit(arrangeSize);
        Child?.Arrange(new Rect((arrangeSize.Width - s.Width) / 2, (arrangeSize.Height - s.Height) / 2, s.Width, s.Height));
        return arrangeSize;
    }
}
