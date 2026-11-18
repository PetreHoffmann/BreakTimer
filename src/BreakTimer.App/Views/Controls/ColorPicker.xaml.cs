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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BreakTimer.App.Views.Controls;

/// <summary>Farbauswahl: Farbfeld, Hex-Eingabe, RGB-Regler und Schnellauswahl. ColorHex ist "#RRGGBB" (TwoWay bindbar).</summary>
public partial class ColorPicker : UserControl
{
    private static readonly string[] PresetColors =
    {
        "#FFFFFF", "#D9E4FF", "#9AA4C7", "#000000", "#0B0716", "#1B0F3D",
        "#18E6FF", "#2D8CFF", "#7B5CFF", "#FF2FB9", "#FF3DAE", "#FF3B63",
        "#FFB84D", "#FFE14D", "#3DDC97", "#8FF0B8", "#14574A", "#4C8DF6"
    };

    public static readonly DependencyProperty ColorHexProperty = DependencyProperty.Register(
        nameof(ColorHex), typeof(string), typeof(ColorPicker),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorPicker)d).OnColorChanged()));

    public static readonly DependencyProperty SwatchProperty = DependencyProperty.Register(nameof(Swatch), typeof(Brush), typeof(ColorPicker));
    public static readonly DependencyProperty SwatchColorProperty = DependencyProperty.Register(nameof(SwatchColor), typeof(Color), typeof(ColorPicker));

    private bool _updating;

    public ColorPicker()
    {
        InitializeComponent();
        foreach (string hex in PresetColors)
        {
            var b = new Button
            {
                Width = 30, Height = 30, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand, Tag = hex, ToolTip = hex,
                Template = BuildSwatchTemplate(), Background = new SolidColorBrush(Parse(hex))
            };
            b.Click += (s, e) => ColorHex = (string)((Button)s).Tag;
            Presets.Children.Add(b);
        }
        OnColorChanged();
    }

    public string ColorHex { get => (string)GetValue(ColorHexProperty); set => SetValue(ColorHexProperty, value); }
    public Brush Swatch { get => (Brush)GetValue(SwatchProperty); private set => SetValue(SwatchProperty, value); }
    public Color SwatchColor { get => (Color)GetValue(SwatchColorProperty); private set => SetValue(SwatchColorProperty, value); }

    private static ControlTemplate BuildSwatchTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        t.VisualTree = border;
        return t;
    }

    private static Color Parse(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Transparent; }
    }

    private void OnColorChanged()
    {
        Color c = Parse(ColorHex);
        if (c == Colors.Transparent && !string.Equals(ColorHex, "#00000000", StringComparison.OrdinalIgnoreCase)) c = Colors.Transparent;
        SwatchColor = Color.FromRgb(c.R, c.G, c.B);
        Swatch = new SolidColorBrush(c);
        if (SliderR == null) return;
        _updating = true;
        SliderR.Value = c.R; SliderG.Value = c.G; SliderB.Value = c.B;
        _updating = false;
        if (HexBox != null && !HexBox.IsKeyboardFocused) HexBox.Text = ColorHex ?? "";
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        ColorHex = $"#{(int)SliderR.Value:X2}{(int)SliderG.Value:X2}{(int)SliderB.Value:X2}";
    }

    private void OnHexCommit(object sender, RoutedEventArgs e)
    {
        string t = (HexBox.Text ?? "").Trim();
        if (t.Length > 0 && t[0] != '#') t = "#" + t;
        bool valid = System.Text.RegularExpressions.Regex.IsMatch(t, "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
        if (valid) ColorHex = t.ToUpperInvariant();
        else HexBox.Text = ColorHex; // ungültige Eingabe verwerfen
    }

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnHexCommit(sender, e); Keyboard.ClearFocus(); }
    }
}
