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
using BreakTimer.Core.Models;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Services;

/// <summary>
/// Gemeinsamer Gestaltungszustand: genau ein "Working"-Theme wird von Break Display, Live-Vorschau und Design Editor
/// verwendet. Änderungen wirken sofort überall. Standard-Themes bleiben unverändert; Änderungen daran lassen sich
/// nur als eigenes Theme speichern.
/// </summary>
public sealed class DesignState : ObservableObject
{
    private Theme _source;
    private bool _isDirty;
    private bool _suspend;

    public DesignState(Theme working, Theme source)
    {
        Working = working ?? throw new ArgumentNullException(nameof(working));
        _source = source;
        _isDirty = source == null || JsonService.Serialize(Normalized(working), false) != JsonService.Serialize(Normalized(source), false);
        Working.PropertyChanged += (s, e) =>
        {
            if (!_suspend) IsDirty = true;
        };
    }

    public Theme Working { get; }

    /// <summary>Theme, von dem das Working-Theme abstammt (kann null sein).</summary>
    public Theme Source
    {
        get => _source;
        private set { if (Set(ref _source, value)) { Raise(nameof(SourceName)); Raise(nameof(CanOverwriteSource)); } }
    }

    public string SourceName => _source?.Name ?? "(unbenannt)";

    /// <summary>Eigene Themes dürfen direkt überschrieben werden, Standard-Themes nicht.</summary>
    public bool CanOverwriteSource => _source != null && !_source.IsBuiltIn;

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public event EventHandler Applied;

    private static Theme Normalized(Theme t)
    {
        Theme c = t.Clone();
        c.Id = ""; c.Name = ""; c.IsBuiltIn = false;
        return c;
    }

    /// <summary>Übernimmt ein Theme als aktuelles Design.</summary>
    public void Apply(Theme source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _suspend = true;
        try { Working.CopyFrom(source); }
        finally { _suspend = false; }
        Source = source;
        IsDirty = false;
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Setzt die Quelle neu, ohne das Working-Theme zu ändern (z. B. nach Import); berechnet den Änderungsstatus neu.</summary>
    public void Rebind(Theme source)
    {
        Source = source;
        IsDirty = source == null || JsonService.Serialize(Normalized(Working), false) != JsonService.Serialize(Normalized(source), false);
    }

    /// <summary>Verwirft Änderungen und stellt das Ausgangs-Theme wieder her.</summary>
    public void Revert()
    {
        if (_source != null) Apply(_source);
    }

    /// <summary>Überschreibt das Quell-Theme (nur eigene Themes).</summary>
    public void SaveToSource(ThemeService themes)
    {
        if (!CanOverwriteSource) throw new InvalidOperationException("Standard-Themes können nicht überschrieben werden.");
        string name = _source.Name;
        _source.CopyFrom(Working);
        _source.Id = Working.Id;
        _source.Name = name;
        _source.IsBuiltIn = false;
        themes.Save(_source);
        MarkSaved(_source);
    }

    /// <summary>Speichert das aktuelle Design als neues eigenes Theme.</summary>
    public Theme SaveAs(ThemeService themes, string name)
    {
        Theme created = themes.SaveAs(Working, name);
        MarkSaved(created);
        return created;
    }

    private void MarkSaved(Theme newSource)
    {
        _suspend = true;
        try { Working.Id = newSource.Id; Working.Name = newSource.Name; Working.IsBuiltIn = false; }
        finally { _suspend = false; }
        Source = newSource;
        IsDirty = false;
    }

    /// <summary>Wird aufgerufen, wenn das Quell-Theme gelöscht wurde.</summary>
    public void DetachSource() { Source = null; IsDirty = true; }
}
