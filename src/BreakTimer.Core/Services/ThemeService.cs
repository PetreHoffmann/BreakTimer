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
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using BreakTimer.Core.Models;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Services;

/// <summary>Verwaltet Standard-Themes (schreibgeschützt) und eigene Themes (JSON-Dateien in AppData\themes).</summary>
public sealed class ThemeService
{
    public const string PackageExtension = ".btheme";
    private readonly AppPaths _paths;
    private readonly ResourceStore _resources;

    public ThemeService(AppPaths paths, ResourceStore resources)
    {
        _paths = paths;
        _resources = resources;
        foreach (Theme t in BuiltInThemes.Create()) Themes.Add(t);
    }

    /// <summary>Alle Themes: zuerst die Standard-Themes, danach die eigenen.</summary>
    public ObservableCollection<Theme> Themes { get; } = new ObservableCollection<Theme>();

    /// <summary>Probleme beim Laden eigener Themes (beschädigte Dateien werden übersprungen).</summary>
    public IList<string> LoadErrors { get; } = new List<string>();

    public Theme Find(string id) => Themes.FirstOrDefault(t => t.Id == id);

    public void LoadCustomThemes()
    {
        LoadErrors.Clear();
        foreach (Theme t in Themes.Where(x => !x.IsBuiltIn).ToList()) Themes.Remove(t);
        try
        {
            _paths.EnsureDirectories();
            foreach (string file in Directory.GetFiles(_paths.ThemesDirectory, "*.json").OrderBy(f => f))
            {
                try
                {
                    Theme t = ReadThemeFile(file);
                    if (Themes.Any(x => x.Id == t.Id)) t.Id = Guid.NewGuid().ToString("N");
                    Themes.Add(t);
                }
                catch (Exception ex)
                {
                    Logger.Error("Theme-Datei ungültig: " + file, ex);
                    LoadErrors.Add(Path.GetFileName(file) + ": " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Themes konnten nicht geladen werden", ex);
            LoadErrors.Add(ex.Message);
        }
    }

    private static Theme ReadThemeFile(string file)
    {
        Theme t = JsonService.Deserialize<Theme>(File.ReadAllText(file, Encoding.UTF8));
        t.IsBuiltIn = false;
        ThemeValidator.Sanitize(t);
        return t;
    }

    private string FileFor(Theme t) => Path.Combine(_paths.ThemesDirectory, t.Id + ".json");

    /// <summary>Speichert ein eigenes Theme. Standard-Themes können nicht überschrieben werden.</summary>
    public void Save(Theme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (theme.IsBuiltIn) throw new InvalidOperationException("Standard-Themes können nicht überschrieben werden. Bitte als eigenes Theme speichern.");
        _paths.EnsureDirectories();
        File.WriteAllText(FileFor(theme), JsonService.Serialize(theme), new UTF8Encoding(false));
        if (!Themes.Contains(theme))
        {
            Theme existing = Find(theme.Id);
            if (existing != null) Themes[Themes.IndexOf(existing)] = theme; else Themes.Add(theme);
        }
    }

    /// <summary>Legt aus beliebigem Quell-Theme (auch Standard-Theme) ein neues eigenes Theme an.</summary>
    public Theme SaveAs(Theme source, string name)
    {
        Theme copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.IsBuiltIn = false;
        copy.Name = UniqueName(string.IsNullOrWhiteSpace(name) ? source.Name : name.Trim());
        Save(copy);
        return copy;
    }

    public Theme Duplicate(Theme source) => SaveAs(source, source.Name + " (Kopie)");

    public void Rename(Theme theme, string newName)
    {
        if (theme.IsBuiltIn) throw new InvalidOperationException("Standard-Themes können nicht umbenannt werden.");
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Der Name darf nicht leer sein.");
        theme.Name = UniqueName(newName.Trim(), theme);
        Save(theme);
    }

    public void Delete(Theme theme)
    {
        if (theme.IsBuiltIn) throw new InvalidOperationException("Standard-Themes können nicht gelöscht werden.");
        string file = FileFor(theme);
        if (File.Exists(file)) File.Delete(file);
        Themes.Remove(theme);
    }

    public string UniqueName(string baseName, Theme ignore = null)
    {
        string name = baseName;
        int i = 2;
        while (Themes.Any(t => t != ignore && string.Equals(t.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            name = baseName + " " + i++;
        return name;
    }

    // ---- Export / Import ----------------------------------------------------------------------

    /// <summary>Exportiert ein Theme als .btheme (ZIP mit theme.json und eingebetteten Bildern) oder als reine .json-Datei.</summary>
    public void Export(Theme theme, string path)
    {
        Theme copy = theme.Clone();
        copy.IsBuiltIn = false;
        if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(path, JsonService.Serialize(copy), new UTF8Encoding(false));
            return;
        }
        if (File.Exists(path)) File.Delete(path);
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            copy.BackgroundImagePath = AddImage(zip, copy.BackgroundImagePath);
            copy.LogoPath = AddImage(zip, copy.LogoPath);
            copy.LottiePath = AddLottie(zip, copy.LottiePath);
            ZipArchiveEntry e = zip.CreateEntry("theme.json");
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(JsonService.Serialize(copy));
        }
    }

    private static string AddLottie(ZipArchive zip, string path)
    {
        if (!ResourceStore.Exists(path)) return "";
        string entryName = "lottie/" + Guid.NewGuid().ToString("N") + ".json";
        zip.CreateEntryFromFile(path, entryName);
        return entryName;
    }

    private string ExtractLottie(ZipArchive zip, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)) return "";
        ZipArchiveEntry e = zip.GetEntry(entryName.Replace('\\', '/'));
        if (e == null || !ResourceStore.IsSupportedLottie(e.Name) || e.Length > ResourceStore.MaxLottieBytes) return "";
        string json;
        using (var r = new StreamReader(e.Open(), Encoding.UTF8)) json = r.ReadToEnd();
        if (!ResourceStore.LooksLikeLottie(json)) return "";
        _paths.EnsureDirectories();
        string target = Path.Combine(_paths.ResourcesDirectory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(target, json, new UTF8Encoding(false));
        return target;
    }

    private static string AddImage(ZipArchive zip, string path)
    {
        if (!ResourceStore.Exists(path)) return "";
        string entryName = "images/" + Guid.NewGuid().ToString("N") + Path.GetExtension(path).ToLowerInvariant();
        zip.CreateEntryFromFile(path, entryName);
        return entryName;
    }

    /// <summary>Importiert .btheme oder .json. Das Theme erhält immer eine neue Id und einen eindeutigen Namen.</summary>
    public Theme Import(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Die Datei wurde nicht gefunden.", path);
        Theme t;
        try
        {
            if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            {
                t = ReadThemeFile(path);
            }
            else
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    ZipArchiveEntry entry = zip.GetEntry("theme.json") ?? throw new InvalidDataException("Die Datei enthält kein theme.json.");
                    string json;
                    using (var r = new StreamReader(entry.Open(), Encoding.UTF8)) json = r.ReadToEnd();
                    t = JsonService.Deserialize<Theme>(json);
                    t.BackgroundImagePath = ExtractImage(zip, t.BackgroundImagePath);
                    t.LogoPath = ExtractImage(zip, t.LogoPath);
                    t.LottiePath = ExtractLottie(zip, t.LottiePath);
                }
            }
        }
        catch (InvalidDataException) { throw; }
        catch (IOException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("Das Theme konnte nicht gelesen werden: " + ex.Message, ex); }

        t.IsBuiltIn = false;
        ThemeValidator.Sanitize(t);
        t.Id = Guid.NewGuid().ToString("N");
        t.Name = UniqueName(t.Name);
        Save(t);
        return t;
    }

    private string ExtractImage(ZipArchive zip, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)) return "";
        ZipArchiveEntry e = zip.GetEntry(entryName.Replace('\\', '/'));
        if (e == null || !ResourceStore.IsSupportedImage(e.Name) || e.Length > ResourceStore.MaxImageBytes) return "";
        _paths.EnsureDirectories();
        string target = Path.Combine(_paths.ResourcesDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(e.Name).ToLowerInvariant());
        using (Stream s = e.Open())
        using (FileStream f = File.Create(target)) s.CopyTo(f);
        return target;
    }
}
