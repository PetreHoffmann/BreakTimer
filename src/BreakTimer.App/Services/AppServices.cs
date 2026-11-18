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
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using BreakTimer.App.ViewModels;
using BreakTimer.App.Views;
using BreakTimer.Core.Models;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Services;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.Services;

/// <summary>Zentrale Zusammensetzung aller Dienste und ViewModels (einfache manuelle Dependency Injection).</summary>
public sealed class AppServices : IDisposable
{
    public AppPaths Paths { get; private set; }
    public AppSettings Settings { get; private set; }
    public SettingsService SettingsService { get; private set; }
    public ThemeService Themes { get; private set; }
    public DesignState Design { get; private set; }
    public MonitorService Monitors { get; private set; }
    public TimerService Timer { get; private set; }
    public DisplayWindowManager Display { get; private set; }
    public SessionController Session { get; private set; }
    public MainViewModel Main { get; private set; }
    public MainWindow Window { get; private set; }

    private MiniControlPresenter _mini;

    public void Start()
    {
        var context = new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher);

        Paths = new AppPaths();
        try { Paths.EnsureDirectories(); } catch (Exception ex) { Logger.Error("Datenordner nicht verfügbar", ex); }
        Logger.FilePath = Paths.LogFile;
        Logger.Info("Break Timer startet");

        SettingsService = new SettingsService(Paths);
        Settings = SettingsService.Load();
        var resources = new ResourceStore(Paths);
        Themes = new ThemeService(Paths, resources);
        Themes.LoadCustomThemes();

        Theme working = Settings.CurrentDesign;
        Theme source = Themes.Find(Settings.ActiveThemeId) ?? Themes.Find(working.Id);
        Design = new DesignState(working, source);

        Monitors = new MonitorService(new ScreenMonitorProvider(), context);
        Monitors.Refresh();
        Monitors.SetPreferred(Settings.DisplayMonitorId, Settings.ControlMonitorId);
        Monitors.StartWatching();

        Timer = new TimerService(context: context);
        Timer.SetDuration(TimeSpan.FromSeconds(Settings.DurationSeconds));

        var preview = new PreviewService();
        var displayVm = new DisplayViewModel(Timer, working, preview) { AnimationsEnabled = Settings.AnimationsEnabled };
        var audio = new AudioService();
        var dialogs = new DialogService();
        Display = new DisplayWindowManager(Monitors, displayVm);
        Session = new SessionController(Timer, Display, Monitors, audio, Settings);

        Action requestSave = () => Main?.RequestSave();
        Action<string, NoticeLevel> notify = (msg, level) => Main?.ShowNotice(msg, level);

        var timerVm = new TimerViewModel(Timer, Session, Display, Settings, requestSave);
        var monitorVm = new MonitorViewModel(Monitors, Settings, requestSave);
        var dashboardVm = new DashboardViewModel(timerVm, monitorVm, Design);
        var editorVm = new DesignEditorViewModel(Design, Themes, resources, preview, timerVm, displayVm, dialogs, notify);
        var themeVm = new ThemeViewModel(Themes, Design, Settings, dialogs, notify, requestSave);
        var settingsVm = new SettingsViewModel(Settings, SettingsService, audio, displayVm, Design, Monitors, Timer, Paths, dialogs, notify,
            requestSave, () => Main?.SaveNow(), AfterSettingsImport);
        var aboutVm = new AboutViewModel(Paths.Root);
        Main = new MainViewModel(Settings, SettingsService, timerVm, dashboardVm, editorVm, themeVm, monitorVm, settingsVm, aboutVm);

        // Änderungen am Design automatisch persistieren.
        working.PropertyChanged += (s, e) => requestSave();
        Design.Applied += (s, e) => requestSave();
        Session.Notice += (s, e) => notify(e.Message, e.Level);
        Monitors.SelectionChanged += (s, e) =>
        {
            Settings.DisplayMonitorId = Monitors.PreferredDisplayId;
            Settings.ControlMonitorId = Monitors.PreferredControlId;
            requestSave();
        };
        Monitors.MonitorsChanged += (s, e) => Main.ShowNotice("Die Monitorkonfiguration hat sich geändert (" + Monitors.Monitors.Count + " Monitor(e) erkannt).", NoticeLevel.Info);

        _mini = new MiniControlPresenter(Display, timerVm);

        Window = new MainWindow(Main, Monitors, id =>
        {
            Monitors.SetPreferredControl(id);
        }, Shutdown);
        Window.GlobalHotkeyPressed = () =>
        {
            if (_mini.BringToFront()) return;
            try
            {
                Window w = Application.Current?.MainWindow;
                if (w == null) return;
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
            }
            catch { }
        };
        Application.Current.MainWindow = Window;
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Window.Show();

        if (SettingsService.LastLoadWarning != null) Main.ShowNotice(SettingsService.LastLoadWarning, NoticeLevel.Warning);
        else if (Themes.LoadErrors.Count > 0) Main.ShowNotice("Einige eigene Themes konnten nicht geladen werden: " + Themes.LoadErrors.First(), NoticeLevel.Warning);
        else if (Monitors.Monitors.Count == 0) Main.ShowNotice("Es wurde kein Monitor erkannt.", NoticeLevel.Warning);
    }

    private void AfterSettingsImport()
    {
        Theme source = Themes.Find(Settings.ActiveThemeId) ?? Themes.Find(Design.Working.Id);
        Design.Rebind(source);
        Timer.SetDuration(TimeSpan.FromSeconds(Settings.DurationSeconds));
        Main.Timer.SyncInputs();
    }

    private void Shutdown()
    {
        // Reihenfolge: Anzeige und Countdown beenden, dann speichern.
        try { Session?.Stop(); } catch { }
        try { Main?.SaveNow(false); } catch { }
    }

    public void Dispose()
    {
        try { Monitors?.Dispose(); } catch { }
        try { Timer?.Dispose(); } catch { }
    }
}
