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
using BreakTimer.App.Views;
using Microsoft.Win32;

namespace BreakTimer.App.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title = "Break Timer");
    void ShowError(string message, string title = "Break Timer");
    bool Confirm(string message, string title = "Break Timer");
    string PromptText(string prompt, string initial, string title = "Break Timer");
    string OpenFile(string filter, string title = null);
    string SaveFile(string filter, string defaultName, string title = null);
}

public sealed class DialogService : IDialogService
{
    private static Window Owner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && !(w is DisplayWindow))
                                   ?? Application.Current?.MainWindow;

    public void ShowInfo(string message, string title = "Break Timer") => Show(message, title, MessageBoxImage.Information);
    public void ShowError(string message, string title = "Break Timer") => Show(message, title, MessageBoxImage.Error);

    private static void Show(string message, string title, MessageBoxImage icon)
    {
        Window owner = Owner;
        if (owner != null) MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
        else MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    public bool Confirm(string message, string title = "Break Timer")
    {
        Window owner = Owner;
        MessageBoxResult r = owner != null
            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return r == MessageBoxResult.Yes;
    }

    public string PromptText(string prompt, string initial, string title = "Break Timer")
    {
        var dlg = new InputDialog(prompt, initial, title) { Owner = Owner };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }

    public string OpenFile(string filter, string title = null)
    {
        var dlg = new OpenFileDialog { Filter = filter, Title = title ?? "Datei auswählen", CheckFileExists = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public string SaveFile(string filter, string defaultName, string title = null)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName, Title = title ?? "Speichern unter", OverwritePrompt = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }
}
