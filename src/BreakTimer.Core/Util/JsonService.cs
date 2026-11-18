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
using System.Text;
using System.Web.Script.Serialization;

namespace BreakTimer.Core.Util;

/// <summary>JSON-Serialisierung ohne externe Abhängigkeiten (JavaScriptSerializer aus dem Framework).</summary>
public static class JsonService
{
    private static JavaScriptSerializer Create() => new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };

    public static string Serialize(object value, bool indented = true)
    {
        string json = Create().Serialize(value);
        return indented ? Indent(json) : json;
    }

    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Leere JSON-Daten.");
        try
        {
            T result = Create().Deserialize<T>(json);
            if (result == null) throw new FormatException("JSON enthält kein gültiges Objekt.");
            return result;
        }
        catch (FormatException) { throw; }
        catch (Exception ex) { throw new FormatException("JSON konnte nicht gelesen werden: " + ex.Message, ex); }
    }

    /// <summary>Einfacher JSON-Formatter für gut lesbare Konfigurationsdateien.</summary>
    public static string Indent(string json)
    {
        var sb = new StringBuilder(json.Length * 2);
        int level = 0;
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < json.Length) sb.Append(json[++i]);
                else if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; sb.Append(c); break;
                case '{':
                case '[':
                    sb.Append(c);
                    if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) break;
                    level++; NewLine(sb, level); break;
                case '}':
                case ']':
                    if (i > 0 && (json[i - 1] == '{' || json[i - 1] == '[')) { sb.Append(c); break; }
                    level--; NewLine(sb, level); sb.Append(c); break;
                case ',': sb.Append(c); NewLine(sb, level); break;
                case ':': sb.Append(": "); break;
                default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static void NewLine(StringBuilder sb, int level) => sb.Append('\n').Append(' ', Math.Max(level, 0) * 2);
}
