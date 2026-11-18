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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BreakTimer.App.Converters;
using BreakTimer.App.ViewModels;
using BreakTimer.Core.Models;

namespace BreakTimer.App.Views.Display;

/// <summary>Basis für Display-Bausteine: abonniert Theme und DisplayViewModel nur solange das Element geladen ist.</summary>
public abstract class DisplayControlBase : UserControl
{
    private DisplayViewModel _vm;

    protected DisplayControlBase()
    {
        Loaded += (s, e) => Attach();
        Unloaded += (s, e) => Detach();
        DataContextChanged += (s, e) => { if (IsLoaded) { Detach(); Attach(); } };
    }

    protected DisplayViewModel Vm => _vm;
    protected Theme Theme => _vm?.Theme;
    protected bool Animations => _vm != null && _vm.AnimationsEnabled;

    private void Attach()
    {
        _vm = DataContext as DisplayViewModel;
        if (_vm == null) return;
        _vm.PropertyChanged += OnVmChanged;
        _vm.Theme.PropertyChanged += OnThemeChanged;
        OnAttached();
        Refresh(null);
    }

    private void Detach()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmChanged;
            _vm.Theme.PropertyChanged -= OnThemeChanged;
        }
        OnDetached();
        _vm = null;
    }

    private void OnVmChanged(object sender, PropertyChangedEventArgs e) => Refresh("vm:" + e.PropertyName);
    private void OnThemeChanged(object sender, PropertyChangedEventArgs e) => Refresh(e.PropertyName);

    protected virtual void OnAttached() { }
    protected virtual void OnDetached() { }

    /// <summary>property == null: alles; "vm:Name": Eigenschaft des ViewModels; sonst Theme-Eigenschaft.</summary>
    protected abstract void Refresh(string property);

    protected static Color Col(string hex, Color fallback) => HexColorConverter.Parse(hex, fallback);

    protected static SolidColorBrush Solid(string hex, byte alpha = 255, Color? fallback = null)
    {
        Color c = HexColorConverter.Parse(hex, fallback ?? Colors.Transparent);
        var b = new SolidColorBrush(Color.FromArgb((byte)(c.A * alpha / 255), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }
}
