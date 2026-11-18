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
using System.ComponentModel;
using System.Linq;
using BreakTimer.Core.Models;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;

namespace BreakTimer.App.ViewModels;

/// <summary>
/// Datenquelle für Break Display UND Live-Vorschau. Es gibt genau eine Instanz; sie liest den einzigen TimerService
/// und das gemeinsame Working-Theme. Dadurch kann keine zweite Timer-Instanz entstehen.
/// </summary>
public sealed class DisplayViewModel : ViewModelBase
{
    private readonly TimerService _timer;
    private readonly PreviewService _preview;
    private string _countdownText = "15:00";
    private string _headingText = "";
    private string _endTimeLine = "";
    private string _footerLine = "";
    private string _companyLine = "";
    private bool _isFinished;
    private bool _animationsEnabled = true;

    public DisplayViewModel(TimerService timer, Theme theme, PreviewService preview)
    {
        _timer = timer;
        _preview = preview;
        Theme = theme;
        timer.Tick += (s, e) => Update();
        timer.StateChanged += (s, e) => Update();
        theme.PropertyChanged += OnThemeChanged;
        preview.PropertyChanged += (s, e) => Update();
        Update();
    }

    public Theme Theme { get; }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set => Set(ref _animationsEnabled, value);
    }

    public string CountdownText { get => _countdownText; private set => Set(ref _countdownText, value); }
    public string HeadingText { get => _headingText; private set => Set(ref _headingText, value); }
    public string EndTimeLine { get => _endTimeLine; private set => Set(ref _endTimeLine, value); }
    public string FooterLine { get => _footerLine; private set => Set(ref _footerLine, value); }
    public string CompanyLine { get => _companyLine; private set => Set(ref _companyLine, value); }
    public bool IsFinished { get => _isFinished; private set => Set(ref _isFinished, value); }

    private static readonly HashSet<string> TextProps = new HashSet<string>
    {
        nameof(Core.Models.Theme.Heading), nameof(Core.Models.Theme.FinishedText), nameof(Core.Models.Theme.EndTimeText),
        nameof(Core.Models.Theme.FooterText), nameof(Core.Models.Theme.Website), nameof(Core.Models.Theme.CompanyName)
    };

    private void OnThemeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (TextProps.Contains(e.PropertyName)) Update();
    }

    private void Update()
    {
        TimeSpan remaining = _timer.Remaining;
        bool finished = _timer.State == TimerState.Finished || _preview.ShowFinished;
        IsFinished = finished;
        CountdownText = finished ? "00:00" : TimeFormatter.Format(remaining);
        HeadingText = finished ? Theme.FinishedText : Theme.Heading;

        string endText = Theme.EndTimeText ?? "";
        EndTimeLine = finished || string.IsNullOrWhiteSpace(endText)
            ? ""
            : endText.Replace("{time}", TimeFormatter.FormatClock(_timer.ExpectedEnd));

        FooterLine = string.Join("   •   ", new[] { Theme.FooterText, Theme.CompanyName, Theme.Website }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        CompanyLine = Theme.CompanyName ?? "";
    }
}

/// <summary>Steuert Vorschau-spezifische Darstellung im Design Editor (z. B. "Pause beendet" ansehen).</summary>
public sealed class PreviewService : ViewModelBase
{
    private bool _showFinished;
    public bool ShowFinished { get => _showFinished; set => Set(ref _showFinished, value); }
}
