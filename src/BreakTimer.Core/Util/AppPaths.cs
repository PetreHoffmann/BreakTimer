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
using System.IO;

namespace BreakTimer.Core.Util;

/// <summary>Alle Dateipfade der Anwendung (standardmäßig unter %AppData%\BreakTimer).</summary>
public sealed class AppPaths
{
    public AppPaths(string root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BreakTimer");
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string SettingsBackupFile => Path.Combine(Root, "settings.json.bak");
    public string ThemesDirectory => Path.Combine(Root, "themes");
    public string ResourcesDirectory => Path.Combine(Root, "resources");
    public string LogFile => Path.Combine(Root, "breaktimer.log");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ThemesDirectory);
        Directory.CreateDirectory(ResourcesDirectory);
    }
}
