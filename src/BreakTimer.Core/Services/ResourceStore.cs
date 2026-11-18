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
using System.Linq;
using BreakTimer.Core.Util;

namespace BreakTimer.Core.Services;

/// <summary>Verwalteter AppData-Ressourcenordner: importierte Bilder werden kopiert, damit die Anwendung nicht von Originalpfaden abhängt.</summary>
public sealed class ResourceStore
{
    public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
    public const long MaxImageBytes = 40L * 1024 * 1024;

    private readonly AppPaths _paths;
    public ResourceStore(AppPaths paths) { _paths = paths; }

    public static bool IsSupportedImage(string path) =>
        !string.IsNullOrEmpty(path) && ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Kopiert ein Bild in den Ressourcenordner und liefert den neuen Pfad. Wirft bei Fehlern IOException/InvalidDataException.</summary>
    public string ImportImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Die Datei wurde nicht gefunden.", sourcePath);
        if (!IsSupportedImage(sourcePath))
            throw new InvalidDataException("Nicht unterstütztes Bildformat. Erlaubt: PNG, JPG, JPEG, BMP, GIF.");
        if (new FileInfo(sourcePath).Length > MaxImageBytes)
            throw new InvalidDataException("Das Bild ist größer als 40 MB.");

        _paths.EnsureDirectories();
        // Bereits verwaltete Dateien nicht erneut kopieren.
        if (IsManaged(sourcePath)) return sourcePath;
        string target = Path.Combine(_paths.ResourcesDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant());
        File.Copy(sourcePath, target, false);
        return target;
    }

    public const long MaxLottieBytes = 20L * 1024 * 1024;

    public static bool IsSupportedLottie(string path) =>
        !string.IsNullOrEmpty(path) && string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Einfache Plausibilitätsprüfung: JSON-Objekt mit Lottie-Schlüsseln (layers, fr, w, h).</summary>
    public static bool LooksLikeLottie(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        string head = json.TrimStart();
        return head.StartsWith("{") && json.Contains("\"layers\"") && json.Contains("\"fr\"") && json.Contains("\"w\"") && json.Contains("\"h\"");
    }

    /// <summary>Kopiert eine Lottie-Datei (.json) in den Ressourcenordner. Wirft bei ungültigen Dateien IOException/InvalidDataException.</summary>
    public string ImportLottie(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Die Datei wurde nicht gefunden.", sourcePath);
        if (!IsSupportedLottie(sourcePath))
            throw new InvalidDataException("Nicht unterstütztes Format. Erlaubt sind Lottie-Dateien (.json).");
        if (new FileInfo(sourcePath).Length > MaxLottieBytes)
            throw new InvalidDataException("Die Lottie-Datei ist größer als 20 MB.");
        if (!LooksLikeLottie(File.ReadAllText(sourcePath)))
            throw new InvalidDataException("Die Datei sieht nicht wie eine Lottie-Animation aus.");

        _paths.EnsureDirectories();
        if (IsManaged(sourcePath)) return sourcePath;
        string target = Path.Combine(_paths.ResourcesDirectory, Guid.NewGuid().ToString("N") + ".json");
        File.Copy(sourcePath, target, false);
        return target;
    }

    public bool IsManaged(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            string dir = Path.GetFullPath(_paths.ResourcesDirectory).TrimEnd('\\') + "\\";
            return Path.GetFullPath(path).StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool Exists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
