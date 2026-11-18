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
using System.Windows.Input;
using System.Windows.Threading;
using BreakTimer.App.Services;
using BreakTimer.Core.Models;
using BreakTimer.Core.Services;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

public sealed class NavItem : ViewModelBase
{
    private bool _isSelected;
    public NavItem(string title, string icon, ViewModelBase page) { Title = title; Icon = icon; Page = page; }
    public string Title { get; }
    public string Icon { get; }
    public ViewModelBase Page { get; }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}

/// <summary>Shell des Control Centers: Navigation, Meldungsbanner, Speichern.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _bannerTimer = new DispatcherTimer();
    private NavItem _current;
    private string _bannerText = "";
    private NoticeLevel _bannerLevel;

    public MainViewModel(AppSettings settings, SettingsService settingsService, TimerViewModel timer,
        DashboardViewModel dashboard, DesignEditorViewModel designEditor, ThemeViewModel themes,
        MonitorViewModel monitors, SettingsViewModel settingsVm, AboutViewModel about)
    {
        _settings = settings;
        _settingsService = settingsService;
        Timer = timer;
        DesignEditor = designEditor;
        Navigation = new List<NavItem>
        {
            new NavItem("Dashboard", "IconDashboard", dashboard),
            new NavItem("Design Editor", "IconPalette", designEditor),
            new NavItem("Themes", "IconLayers", themes),
            new NavItem("Monitore", "IconMonitor", monitors),
            new NavItem("Einstellungen", "IconSettings", settingsVm),
            new NavItem("Über Break Timer", "IconInfo", about)
        };
        NavigateCommand = new RelayCommand(p => Navigate(p as NavItem));
        DismissBannerCommand = new RelayCommand(() => BannerText = "");
        _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); SaveNow(false); };
        _bannerTimer.Tick += (s, e) => { _bannerTimer.Stop(); BannerText = ""; };
        Navigate(Navigation[0]);
    }

    public TimerViewModel Timer { get; }
    public DesignEditorViewModel DesignEditor { get; }
    public IList<NavItem> Navigation { get; }
    public ICommand NavigateCommand { get; }
    public ICommand DismissBannerCommand { get; }

    public NavItem Current
    {
        get => _current;
        private set => Set(ref _current, value);
    }

    public ViewModelBase CurrentPage => _current?.Page;

    public string BannerText
    {
        get => _bannerText;
        private set { if (Set(ref _bannerText, value)) Raise(nameof(HasBanner)); }
    }

    public bool HasBanner => !string.IsNullOrEmpty(_bannerText);
    public NoticeLevel BannerLevel { get => _bannerLevel; private set { Set(ref _bannerLevel, value); Raise(nameof(BannerLevelName)); } }
    public string BannerLevelName => _bannerLevel.ToString();

    public void Navigate(NavItem item)
    {
        if (item == null || item == _current) return;
        if (_current?.Page == DesignEditor) DesignEditor.OnLeave();
        foreach (NavItem n in Navigation) n.IsSelected = n == item;
        Current = item;
        Raise(nameof(CurrentPage));
    }

    public void NavigateTo(string title)
    {
        foreach (NavItem n in Navigation) if (n.Title == title) { Navigate(n); return; }
    }

    public void ShowNotice(string message, NoticeLevel level = NoticeLevel.Info)
    {
        BannerLevel = level;
        BannerText = message;
        _bannerTimer.Stop();
        _bannerTimer.Interval = TimeSpan.FromSeconds(level == NoticeLevel.Info ? 5 : 12);
        _bannerTimer.Start();
    }

    /// <summary>Speichert verzögert (bündelt viele Änderungen, z. B. beim Ziehen eines Reglers).</summary>
    public void RequestSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public bool SaveNow(bool announce = true)
    {
        _saveTimer.Stop();
        bool ok = _settingsService.TrySave(_settings, out string error);
        if (!ok) ShowNotice("Einstellungen konnten nicht gespeichert werden: " + error, NoticeLevel.Error);
        else if (announce) ShowNotice("Einstellungen gespeichert.", NoticeLevel.Info);
        return ok;
    }
}

public sealed class AboutViewModel : ViewModelBase
{
    public AboutViewModel(string dataFolder) { DataFolder = dataFolder; }
    public string Version { get; } = "Version " + typeof(AboutViewModel).Assembly.GetName().Version.ToString(3);
    public string DataFolder { get; }

    public string Author => "Petre Hoffmann";
    public string DevelopedBy => "Entwickelt von " + Author;
    public string Copyright => "© " + DateTime.Now.Year + " " + Author;

    /// <summary>GitHub-Repository. Leer lassen, solange es keins gibt – dann wird kein Link angezeigt.</summary>
    public string RepositoryUrl { get; } = "";
    public bool HasRepository => !string.IsNullOrWhiteSpace(RepositoryUrl);
    public ICommand OpenRepositoryCommand => new RelayCommand(() => OpenUrl(RepositoryUrl));
    public ICommand OpenUrlCommand { get; } = new RelayCommand(p => OpenUrl(p as string));

    private static void OpenUrl(string url)
    {
        // Nur http(s)-Adressen öffnen.
        if (string.IsNullOrWhiteSpace(url) || !(url.StartsWith("https://") || url.StartsWith("http://"))) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { BreakTimer.Core.Util.Logger.Error("Link konnte nicht geöffnet werden: " + url, ex); }
    }

    public string OwnLicenseText => "Break Timer steht unter der Apache License 2.0.";
    public string OwnLicenseUrl => "https://www.apache.org/licenses/LICENSE-2.0";

    /// <summary>Verwendete Drittanbieter-Komponenten (nur diese werden mit der Anwendung ausgeliefert).</summary>
    public IReadOnlyList<DependencyInfo> Dependencies { get; } = new[]
    {
        new DependencyInfo("SkiaSharp", "2.88.6", "MIT", "https://github.com/mono/SkiaSharp", "https://github.com/mono/SkiaSharp/blob/main/LICENSE.md", "Zeichnen der Lottie-Animationen (.NET-Anbindung)"),
        new DependencyInfo("SkiaSharp.Skottie", "2.88.6", "MIT", "https://github.com/mono/SkiaSharp", "https://github.com/mono/SkiaSharp/blob/main/LICENSE.md", "Lottie-Wiedergabe (Skottie)"),
        new DependencyInfo("Skia (native Bibliothek)", "in SkiaSharp", "BSD 3-Clause", "https://skia.org/", "https://github.com/google/skia/blob/main/LICENSE", "Grafikbibliothek von Google, enthalten in libSkiaSharp.dll"),
        new DependencyInfo("System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe", "", "MIT", "https://github.com/dotnet/runtime", "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT", "Hilfsbibliotheken von Microsoft für SkiaSharp"),
    };

    public IReadOnlyList<KeyValuePair<string, string>> Shortcuts { get; } = new[]
    {
        new KeyValuePair<string, string>("Leertaste", "Start bzw. Pause / Fortsetzen"),
        new KeyValuePair<string, string>("Esc", "Stop – beendet Pause und Display (oder die Testanzeige)"),
        new KeyValuePair<string, string>("Strg + S", "Einstellungen speichern"),
        new KeyValuePair<string, string>("+", "Eine Minute hinzufügen"),
        new KeyValuePair<string, string>("–", "Eine Minute abziehen"),
        new KeyValuePair<string, string>("Strg + R", "Timer zurücksetzen"),
        new KeyValuePair<string, string>("Strg + Alt + B", "Global: Mini-Steuerung bzw. Control Center in den Vordergrund holen"),
    };
}

public sealed class DependencyInfo
{
    public DependencyInfo(string name, string version, string license, string projectUrl, string licenseUrl, string purpose)
    { Name = name; Version = version; License = license; ProjectUrl = projectUrl; LicenseUrl = licenseUrl; Purpose = purpose; }
    public string Name { get; }
    public string Version { get; }
    public string License { get; }
    public string ProjectUrl { get; }
    public string LicenseUrl { get; }
    public string Purpose { get; }
    public string Title => string.IsNullOrEmpty(Version) ? Name : Name + "  " + Version;
}
