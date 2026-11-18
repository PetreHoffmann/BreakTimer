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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Services;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

public sealed class ThemeItemViewModel : ViewModelBase
{
    private bool _isActive;
    public ThemeItemViewModel(Theme theme) { Theme = theme; }
    public Theme Theme { get; }
    public string Name => Theme.Name;
    public bool IsBuiltIn => Theme.IsBuiltIn;
    public string Kind => Theme.IsBuiltIn ? "Standard" : "Eigenes Theme";
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
    public string Background => Theme.BackgroundColor;
    public string GradientStart => Theme.GradientEnabled ? Theme.GradientStart : Theme.BackgroundColor;
    public string GradientEnd => Theme.GradientEnabled ? Theme.GradientEnd : Theme.BackgroundColor;
    public string Accent => Theme.AccentColor;
    public string Secondary => Theme.SecondaryColor;
    public string Sample => Theme.Heading;
    public void RefreshName() => Raise(nameof(Name));
}

/// <summary>Theme-Verwaltung: auswählen, duplizieren, umbenennen, löschen, importieren, exportieren.</summary>
public sealed class ThemeViewModel : ViewModelBase
{
    private const string PackageFilter = "Break-Timer-Theme (*.btheme)|*.btheme|JSON (*.json)|*.json";
    private readonly ThemeService _themes;
    private readonly DesignState _design;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly Action<string, NoticeLevel> _notify;
    private readonly Action _requestSave;

    public ThemeViewModel(ThemeService themes, DesignState design, AppSettings settings, IDialogService dialogs,
        Action<string, NoticeLevel> notify, Action requestSave)
    {
        _themes = themes;
        _design = design;
        _settings = settings;
        _dialogs = dialogs;
        _notify = notify;
        _requestSave = requestSave;

        themes.Themes.CollectionChanged += OnThemesChanged;
        design.PropertyChanged += (s, e) => UpdateActive();
        design.Applied += (s, e) => UpdateActive();

        ApplyCommand = new RelayCommand(p => Apply(p as ThemeItemViewModel));
        DuplicateCommand = new RelayCommand(p => Run(() => { var t = _themes.Duplicate(((ThemeItemViewModel)p).Theme); Say($"Theme „{t.Name}“ wurde angelegt."); }));
        RenameCommand = new RelayCommand(p => Rename(p as ThemeItemViewModel), p => p is ThemeItemViewModel i && !i.IsBuiltIn);
        DeleteCommand = new RelayCommand(p => Delete(p as ThemeItemViewModel), p => p is ThemeItemViewModel i && !i.IsBuiltIn);
        ExportCommand = new RelayCommand(p => Export(p as ThemeItemViewModel));
        ImportCommand = new RelayCommand(Import);
        SaveCurrentAsCommand = new RelayCommand(() => Run(() =>
        {
            string name = _dialogs.PromptText("Name des neuen Themes:", _themes.UniqueName(_design.SourceName + " (angepasst)"), "Aktuelles Design speichern");
            if (name == null) return;
            Theme t = _design.SaveAs(_themes, name);
            Say($"Theme „{t.Name}“ wurde gespeichert.");
        }));
        Rebuild();
    }

    public ObservableCollection<ThemeItemViewModel> Items { get; } = new ObservableCollection<ThemeItemViewModel>();
    public ICommand ApplyCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand SaveCurrentAsCommand { get; }

    private void Say(string msg, NoticeLevel level = NoticeLevel.Info) => _notify(msg, level);

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Logger.Error("Theme-Aktion fehlgeschlagen", ex);
            Say("Aktion fehlgeschlagen: " + ex.Message, NoticeLevel.Error);
        }
    }

    private void OnThemesChanged(object sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Items.Clear();
        foreach (Theme t in _themes.Themes) Items.Add(new ThemeItemViewModel(t));
        UpdateActive();
    }

    private void UpdateActive()
    {
        foreach (ThemeItemViewModel i in Items) i.IsActive = ReferenceEquals(i.Theme, _design.Source);
    }

    private void Apply(ThemeItemViewModel item)
    {
        if (item == null) return;
        Run(() =>
        {
            _design.Apply(item.Theme);
            _settings.ActiveThemeId = item.Theme.Id;
            _requestSave();
            Say($"Theme „{item.Name}“ aktiviert.");
        });
    }

    private void Rename(ThemeItemViewModel item)
    {
        if (item == null || item.IsBuiltIn) return;
        Run(() =>
        {
            string name = _dialogs.PromptText("Neuer Name:", item.Name, "Theme umbenennen");
            if (name == null) return;
            _themes.Rename(item.Theme, name);
            if (ReferenceEquals(_design.Source, item.Theme)) _design.Working.Name = item.Theme.Name;
            item.RefreshName();
        });
    }

    private void Delete(ThemeItemViewModel item)
    {
        if (item == null || item.IsBuiltIn) return;
        Run(() =>
        {
            if (!_dialogs.Confirm($"Theme „{item.Name}“ wirklich löschen?")) return;
            bool wasSource = ReferenceEquals(_design.Source, item.Theme);
            _themes.Delete(item.Theme);
            if (wasSource) { _design.DetachSource(); _settings.ActiveThemeId = ""; _requestSave(); }
            Say("Theme gelöscht.");
        });
    }

    private void Export(ThemeItemViewModel item)
    {
        if (item == null) return;
        Run(() =>
        {
            string path = _dialogs.SaveFile(PackageFilter, item.Name + ThemeService.PackageExtension, "Theme exportieren");
            if (path == null) return;
            _themes.Export(item.Theme, path);
            Say($"Theme „{item.Name}“ exportiert.");
        });
    }

    private void Import()
    {
        Run(() =>
        {
            string path = _dialogs.OpenFile(PackageFilter + "|Alle Dateien|*.*", "Theme importieren");
            if (path == null) return;
            Theme t = _themes.Import(path);
            Say($"Theme „{t.Name}“ importiert.");
        });
    }
}
