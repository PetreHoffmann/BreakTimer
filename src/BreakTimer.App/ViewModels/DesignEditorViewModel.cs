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
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using BreakTimer.App.Converters;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Services;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

/// <summary>Design Editor: bearbeitet das gemeinsame Working-Theme; die Live-Vorschau bindet an dieselbe Instanz.</summary>
public sealed class DesignEditorViewModel : ViewModelBase
{
    private const string LottieFilter = "Lottie-Animation (*.json)|*.json|Alle Dateien|*.*";
    private const string ImageFilter = "Bilder (PNG, JPG, JPEG, BMP, GIF)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Alle Dateien|*.*";

    private readonly DesignState _design;
    private readonly ThemeService _themes;
    private readonly ResourceStore _resources;
    private readonly PreviewService _preview;
    private readonly IDialogService _dialogs;
    private readonly Action<string, NoticeLevel> _notify;

    public DesignEditorViewModel(DesignState design, ThemeService themes, ResourceStore resources, PreviewService preview,
        TimerViewModel timer, DisplayViewModel display, IDialogService dialogs, Action<string, NoticeLevel> notify)
    {
        _design = design;
        _themes = themes;
        _resources = resources;
        _preview = preview;
        _dialogs = dialogs;
        _notify = notify;
        Timer = timer;
        Display = display;

        design.PropertyChanged += (s, e) => { Raise(nameof(IsDirty)); Raise(nameof(SourceName)); Raise(nameof(Hint)); Raise(nameof(CanOverwrite)); };
        design.Working.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Theme.BackgroundImagePath)) Raise(nameof(HasBackgroundImage));
            if (e.PropertyName == nameof(Theme.LottiePath)) { Raise(nameof(HasLottie)); Raise(nameof(LottieName)); }
            if (e.PropertyName == nameof(Theme.LogoPath)) Raise(nameof(HasLogo));
        };

        FontFamilies = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

        ImportBackgroundCommand = new RelayCommand(() => ImportImage(true));
        RemoveBackgroundCommand = new RelayCommand(() => Theme.BackgroundImagePath = "");
        ImportLottieCommand = new RelayCommand(ImportLottie);
        RemoveLottieCommand = new RelayCommand(() => Theme.LottiePath = "");
        ImportLogoCommand = new RelayCommand(() => ImportImage(false));
        RemoveLogoCommand = new RelayCommand(() => Theme.LogoPath = "");
        SaveCommand = new RelayCommand(Save);
        SaveAsCommand = new RelayCommand(SaveAs);
        RevertCommand = new RelayCommand(Revert, () => _design.Source != null && _design.IsDirty);
        ResetLayoutCommand = new RelayCommand(ResetLayout);
    }

    public Theme Theme => _design.Working;
    public TimerViewModel Timer { get; }
    public DisplayViewModel Display { get; }

    public IReadOnlyList<string> FontFamilies { get; }
    public IReadOnlyList<string> FontWeights { get; } = new[] { "Thin", "ExtraLight", "Light", "SemiLight", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" };
    public IReadOnlyList<string> Alignments { get; } = new[] { "Left", "Center", "Right" };
    public IReadOnlyList<string> LogoPositions { get; } = new[] { "Left", "Right", "Top" };
    public IReadOnlyList<string> Stretches { get; } = new[] { "UniformToFill", "Uniform", "Fill", "None" };
    public IReadOnlyList<string> Styles { get; } = new[] { "Orbit", "Grid", "Nature", "Corporate", "None" };

    public ICommand ImportBackgroundCommand { get; }
    public ICommand RemoveBackgroundCommand { get; }
    public ICommand ImportLogoCommand { get; }
    public ICommand RemoveLogoCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand ResetLayoutCommand { get; }

    public bool IsDirty => _design.IsDirty;
    public string SourceName => _design.SourceName;
    public bool CanOverwrite => _design.CanOverwriteSource;
    public ICommand ImportLottieCommand { get; }
    public ICommand RemoveLottieCommand { get; }
    public bool HasLottie => !string.IsNullOrWhiteSpace(Theme.LottiePath);
    public string LottieName => HasLottie ? (File.Exists(Theme.LottiePath) ? "Lottie-Animation geladen" : "Datei nicht gefunden") : "Keine Animation gewählt";
    public IReadOnlyList<string> LottieLayers { get; } = new[] { "Behind", "Front" };
    public IReadOnlyList<string> LottieFits { get; } = new[] { "Uniform", "UniformToFill" };
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(Theme.BackgroundImagePath);
    public bool HasLogo => !string.IsNullOrWhiteSpace(Theme.LogoPath);

    public string Hint
    {
        get
        {
            if (_design.Source == null) return "Das Design wurde noch nicht als Theme gespeichert.";
            if (_design.Source.IsBuiltIn)
                return _design.IsDirty
                    ? $"Änderungen an „{_design.SourceName}“ (Standard-Theme, schreibgeschützt): Mit „Als Theme speichern“ als eigenes Theme sichern."
                    : $"Aktuelles Theme: „{_design.SourceName}“ (Standard-Theme). Änderungen werden automatisch gespeichert, das Standard-Theme bleibt unverändert.";
            return _design.IsDirty ? $"Ungespeicherte Änderungen an „{_design.SourceName}“." : $"Aktuelles Theme: „{_design.SourceName}“.";
        }
    }

    /// <summary>Zeigt in der Vorschau den Zustand "Pause beendet".</summary>
    public bool PreviewFinished
    {
        get => _preview.ShowFinished;
        set { _preview.ShowFinished = value; Raise(); }
    }

    /// <summary>Wird beim Verlassen der Seite aufgerufen.</summary>
    public void OnLeave()
    {
        if (_preview.ShowFinished) { _preview.ShowFinished = false; Raise(nameof(PreviewFinished)); }
    }

    private void ImportImage(bool background)
    {
        try
        {
            string file = _dialogs.OpenFile(ImageFilter, background ? "Hintergrundbild wählen" : "Logo wählen");
            if (file == null) return;
            string managed = _resources.ImportImage(file);
            if (PathToImageConverter.Load(managed) == null)
            {
                try { if (_resources.IsManaged(managed)) File.Delete(managed); } catch { }
                throw new InvalidDataException("Die Datei konnte nicht als Bild gelesen werden.");
            }
            if (background) Theme.BackgroundImagePath = managed; else { Theme.LogoPath = managed; Theme.LogoVisible = true; }
        }
        catch (Exception ex)
        {
            Logger.Error("Bildimport fehlgeschlagen", ex);
            _notify("Bild konnte nicht importiert werden: " + ex.Message, NoticeLevel.Error);
        }
    }

    private void ImportLottie()
    {
        try
        {
            string file = _dialogs.OpenFile(LottieFilter, "Lottie-Animation wählen");
            if (file == null) return;
            string managed = _resources.ImportLottie(file);
            Theme.LottiePath = managed;
        }
        catch (Exception ex)
        {
            Logger.Error("Lottie-Import fehlgeschlagen", ex);
            _notify("Lottie-Animation konnte nicht importiert werden: " + ex.Message, NoticeLevel.Error);
        }
    }

    private void Save()
    {
        try
        {
            if (_design.CanOverwriteSource)
            {
                _design.SaveToSource(_themes);
                _notify($"Theme „{_design.SourceName}“ gespeichert.", NoticeLevel.Info);
            }
            else SaveAs();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void SaveAs()
    {
        try
        {
            string suggestion = _themes.UniqueName((_design.Source?.Name ?? "Mein Theme") + " (angepasst)");
            string name = _dialogs.PromptText("Name des neuen Themes:", suggestion, "Als Theme speichern");
            if (name == null) return;
            Theme created = _design.SaveAs(_themes, name);
            _notify($"Theme „{created.Name}“ wurde gespeichert.", NoticeLevel.Info);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Revert()
    {
        _design.Revert();
        _notify("Änderungen verworfen.", NoticeLevel.Info);
    }

    private void ResetLayout()
    {
        var d = new Theme();
        Theme t = Theme;
        t.HeaderPosition = d.HeaderPosition; t.FooterPosition = d.FooterPosition; t.IllustrationPosition = d.IllustrationPosition;
        t.CountdownOffsetX = 0; t.CountdownOffsetY = 0; t.IllustrationOffsetX = 0; t.IllustrationOffsetY = 0;
        t.IllustrationScale = 1; t.IllustrationSpeed = 1; t.HorizontalPadding = d.HorizontalPadding; t.VerticalPadding = d.VerticalPadding;
        t.MainScale = 1; t.HeaderScale = 1; t.FooterScale = 1; t.TextAlignment = d.TextAlignment;
    }

    private void Fail(Exception ex)
    {
        Logger.Error("Theme speichern fehlgeschlagen", ex);
        _notify("Das Theme konnte nicht gespeichert werden: " + ex.Message, NoticeLevel.Error);
    }
}
