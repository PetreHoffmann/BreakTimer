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
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Converters;

/// <summary>Hex-String (#RRGGBB / #AARRGGBB) nach Color oder Brush. Ungültige Werte ergeben Transparent.</summary>
public sealed class HexColorConverter : IValueConverter
{
    public static Color Parse(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Color c = Parse(value as string, Colors.Transparent);
        if (targetType == typeof(Color) || targetType == typeof(Color?)) return c;
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color c) return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}".Replace("#FF", "#");
        if (value is SolidColorBrush b) return $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
        return Binding.DoNothing;
    }
}

/// <summary>Dateipfad nach ImageSource (fehlende oder defekte Dateien ergeben null, nie eine Ausnahme).</summary>
public sealed class PathToImageConverter : IValueConverter
{
    public static ImageSource Load(string path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
            bmp.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            Logger.Error("Bild konnte nicht geladen werden: " + path, ex);
            return null;
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Load(value as string, 3840);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>true -> Visible. Parameter "invert" kehrt um. Strings: nicht leer = true. Objekte: nicht null = true.</summary>
public sealed class VisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool v;
        if (value is bool b) v = b;
        else if (value is string s) v = !string.IsNullOrWhiteSpace(s);
        else if (value is int i) v = i != 0;
        else v = value != null;
        if (parameter as string == "invert") v = !v;
        return v ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
}

/// <summary>Double-Faktor aus Prozent / Skalierung (value * parameter).</summary>
public sealed class MultiplyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double v = value is double d ? d : 0;
        double f = parameter == null ? 1 : double.Parse(parameter.ToString(), CultureInfo.InvariantCulture);
        return v * f;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Pfad vorhanden? Für Warnhinweise im Editor.</summary>
public sealed class FileMissingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string p = value as string;
        return !string.IsNullOrWhiteSpace(p) && !File.Exists(p) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>TimerState nach Farbe des Statuspunkts.</summary>
public sealed class StatusLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = (value as string) switch
        {
            "Ok" => "SuccessBrush",
            "Warn" => "WarnBrush",
            "Error" => "DangerBrush",
            "Active" => "AccentBrush",
            _ => "TextDimBrush"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Aus (horizontal, vertikal) wird ein Thickness. Parameter: "h" nur seitlich, "v" nur oben/unten, sonst beides.</summary>
public sealed class PaddingMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double h = values.Length > 0 && values[0] is double a ? a : 0;
        double v = values.Length > 1 && values[1] is double b ? b : 0;
        switch (parameter as string)
        {
            case "h": return new Thickness(h, 0, h, 0);
            case "v": return new Thickness(0, v, 0, v);
            default: return new Thickness(h, v, h, v);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Produkt aller double-Werte, multipliziert mit dem Parameter (Standard 1).</summary>
public sealed class ProductMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double p = parameter == null ? 1 : double.Parse(parameter.ToString(), CultureInfo.InvariantCulture);
        foreach (object o in values) p *= o is double d ? d : 1;
        return p;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Schlüsselname -> Anwendungsressource (z. B. Icon-Geometrie).</summary>
public sealed class ResourceLookupConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key ? Application.Current.TryFindResource(key) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Zeigt, ob zwei Werte gleich sind (für Auswahl-Hervorhebung).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value?.ToString(), parameter?.ToString());
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? parameter : Binding.DoNothing;
}
