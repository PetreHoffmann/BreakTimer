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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BreakTimer.App.Views.Controls;

/// <summary>
/// Countdown mit garantiert gleicher Ziffernbreite: jede Ziffer steht in einer Zelle fester Breite,
/// dadurch "wackelt" die Anzeige nicht, wenn sich Ziffern ändern - unabhängig von der gewählten Schrift.
/// </summary>
public sealed class CountdownControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CountdownControl), new PropertyMetadata("00:00", (d, e) => ((CountdownControl)d).Rebuild()));

    private readonly StackPanel _panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private double _digitWidth = 10, _colonWidth = 5;
    private bool _metricsDirty = true;

    public CountdownControl()
    {
        Content = _panel;
        Focusable = false;
        IsHitTestVisible = false;
        Loaded += (s, e) => { _metricsDirty = true; Rebuild(); };
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontFamilyProperty || e.Property == FontSizeProperty || e.Property == FontWeightProperty || e.Property == FontStyleProperty)
        {
            _metricsDirty = true;
            Rebuild();
        }
    }

    private void Measure()
    {
        double dpi = 1.0;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
        var face = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);
        double size = Math.Max(1, FontSize);
        double max = 0;
        for (char c = '0'; c <= '9'; c++)
            max = Math.Max(max, new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.Black, dpi).WidthIncludingTrailingWhitespace);
        _digitWidth = Math.Ceiling(max);
        _colonWidth = Math.Ceiling(new FormattedText(":", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.Black, dpi).WidthIncludingTrailingWhitespace);
        _metricsDirty = false;
    }

    private void Rebuild()
    {
        if (_panel == null) return;
        if (_metricsDirty) Measure();
        string text = Text ?? "";

        // Struktur neu aufbauen, wenn sich die Zeichenfolge-Struktur ändert (Länge / Doppelpunkt-Positionen).
        bool sameShape = _panel.Children.Count == text.Length;
        if (sameShape)
        {
            for (int i = 0; i < text.Length; i++)
                if ((((TextBlock)_panel.Children[i]).Tag as string == ":") != (text[i] == ':')) { sameShape = false; break; }
        }
        if (!sameShape)
        {
            _panel.Children.Clear();
            foreach (char c in text)
                _panel.Children.Add(new TextBlock { TextAlignment = TextAlignment.Center, Tag = c == ':' ? ":" : "d" });
        }
        for (int i = 0; i < text.Length; i++)
        {
            var tb = (TextBlock)_panel.Children[i];
            tb.Text = text[i].ToString();
            tb.Width = text[i] == ':' ? _colonWidth : _digitWidth;
        }
    }
}
