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
using System.Threading;
using System.Windows;

namespace BreakTimer.App;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        // Nur eine Instanz: verhindert doppelte Timer und doppelte Display-Fenster.
        using (var mutex = new Mutex(true, @"Local\BreakTimer.SingleInstance", out bool created))
        {
            if (!created)
            {
                MessageBox.Show("Break Timer läuft bereits. Bitte verwenden Sie das geöffnete Control Center.",
                    "Break Timer", MessageBoxButton.OK, MessageBoxImage.Information);
                return 0;
            }
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
