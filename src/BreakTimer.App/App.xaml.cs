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
using System.Windows.Threading;
using BreakTimer.App.Services;
using BreakTimer.Core.Util;

namespace BreakTimer.App;

public partial class App : Application
{
    private AppServices _services;

    /// <summary>Zeigt einen Fehler im Control Center (Banner) oder, falls nicht verfügbar, als Meldung. Wirft nie.</summary>
    public static void ReportError(string message, Exception ex = null)
    {
        try
        {
            string text = ex == null ? message : message + " (" + ex.Message + ")";
            if (Current is App app && app._services?.Main != null) app._services.Main.ShowNotice(text, NoticeLevel.Error);
            else MessageBox.Show(text, "Break Timer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (s, a) => Logger.Error("Unbehandelte Ausnahme", a.ExceptionObject as Exception);

        try
        {
            _services = new AppServices();
            _services.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Start fehlgeschlagen", ex);
            MessageBox.Show("Break Timer konnte nicht gestartet werden:\n\n" + ex.Message, "Break Timer", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Die Anwendung darf wegen einzelner Fehler nicht abstürzen; vor allem STOP muss erreichbar bleiben.
        Logger.Error("Fehler in der Oberfläche", e.Exception);
        e.Handled = true;
        ReportError("Ein unerwarteter Fehler ist aufgetreten und wurde abgefangen.", e.Exception);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _services?.Dispose(); } catch { }
        base.OnExit(e);
    }
}
