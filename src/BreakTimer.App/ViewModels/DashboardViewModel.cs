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

using BreakTimer.Core.Services;

namespace BreakTimer.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    public DashboardViewModel(TimerViewModel timer, MonitorViewModel monitors, DesignState design)
    {
        Timer = timer;
        Monitors = monitors;
        Design = design;
        design.PropertyChanged += (s, e) => Raise(nameof(ThemeName));
    }

    public TimerViewModel Timer { get; }
    public MonitorViewModel Monitors { get; }
    public DesignState Design { get; }
    public string ThemeName => Design.SourceName + (Design.IsDirty ? " (geändert)" : "");
    public int[] Presets { get; } = { 5, 10, 15, 20, 25, 30, 45, 60 };
}
