using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Calculator.Engine;

namespace CalcApp.Localization;

/// <summary>
/// Localization service. Strings come from JSON tables generated from the original .resw files
/// (see tools/gen_strings.py) and fall back to en-US. Also provides the calculation engine strings
/// and the regional number format (decimal / grouping separators).
/// </summary>
public sealed class Loc : IResourceProvider
{
    private static readonly Dictionary<string, string> s_alias = new Dictionary<string, string>
    {
        ["Standard"] = "StandardModeText",
        ["Scientific"] = "ScientificModeText",
        ["Programmer"] = "ProgrammerModeText",
        ["DateCalculation"] = "DateCalculationModeText",
        ["History"] = "HistoryLabel.Text",
        ["Memory"] = "MemoryLabel.Text",
        ["HistoryEmpty"] = "HistoryEmpty.Text",
        ["MemoryEmpty"] = "MemoryPaneEmpty.Text",
        ["ClearHistory"] = "ClearHistory.Name",
        ["ClearMemory"] = "ClearMemoryMenuItem.Text",
        ["AddSubtract"] = "Date_AddSubtractOption.Content",
        ["Difference"] = "Date_DifferenceOption.Content",
        ["Add"] = "AddOption.Content",
        ["Subtract"] = "SubtractOption.Content",
        ["From"] = "DateDiff_FromHeader.Header",
        ["To"] = "DateDiff_ToHeader.Header",
        ["FromAddSubtract"] = "AddSubtract_Date_FromHeader.Header",
        ["DifferenceLabel"] = "Date_DifferenceLabel.Text",
        ["Years"] = "YearsLabel.Text",
        ["Months"] = "MonthsLabel.Text",
        ["Days"] = "DaysLabel.Text",
        ["DateLabel"] = "DateLabel.Text",
        ["Settings"] = "SettingsHeader.Text",
        ["Appearance"] = "SettingsAppearance.Text",
    };

    // Used when neither the current locale nor en-US has the key.
    private static readonly Dictionary<string, string> s_hardcoded = new Dictionary<string, string>
    {
        ["Copy"] = "Copy",
        ["Paste"] = "Paste",
        ["Converter"] = "Converter",
        ["Calculator"] = "Calculator",
        ["Menu"] = "Menu",
        ["BitFlip"] = "Bit toggle keypad",
        ["Keypad"] = "Keypad",
    };

    private Dictionary<string, string> _current = new Dictionary<string, string>();
    private Dictionary<string, string> _fallback = new Dictionary<string, string>();
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    public static Loc Instance { get; } = new Loc();

    public string Locale { get; private set; } = "en-US";

    public IReadOnlyList<string> AvailableLocales { get; private set; } = new List<string> { "en-US" };

    public CultureInfo Culture => _culture;

    private Loc()
    {
        try
        {
            AvailableLocales = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && n.Contains(".Strings."))
                .Select(n => n.Substring(n.IndexOf(".Strings.", StringComparison.Ordinal) + ".Strings.".Length))
                .Select(n => n.Substring(0, n.Length - ".json".Length))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            AvailableLocales = new List<string> { "en-US" };
        }

        _fallback = Load("en-US");
        string? name = null;
        try
        {
            _culture = CultureInfo.CurrentUICulture;
            name = _culture.Name;
            CultureInfo regional = CultureInfo.CurrentCulture;
            _culture = regional;
        }
        catch
        {
            _culture = CultureInfo.InvariantCulture;
        }

        SetLocale(name ?? "en-US");
    }

    public void SetLocale(string cultureName)
    {
        string chosen = Resolve(cultureName);
        Locale = chosen;
        _current = chosen == "en-US" ? _fallback : Load(chosen);
    }

    private string Resolve(string cultureName)
    {
        if (string.IsNullOrEmpty(cultureName))
        {
            return "en-US";
        }

        string? exact = AvailableLocales.FirstOrDefault(l => string.Equals(l, cultureName, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        string lang = cultureName.Split('-')[0];
        if (lang == "zh")
        {
            bool traditional = cultureName.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0
                || cultureName.EndsWith("TW", StringComparison.OrdinalIgnoreCase)
                || cultureName.EndsWith("HK", StringComparison.OrdinalIgnoreCase);
            return traditional ? "zh-TW" : "zh-CN";
        }

        string? sameLang = AvailableLocales.FirstOrDefault(l => l.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase));
        return sameLang ?? "en-US";
    }

    private static Dictionary<string, string> Load(string locale)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".Strings." + locale + ".json", StringComparison.OrdinalIgnoreCase));
            if (resName == null)
            {
                return new Dictionary<string, string>();
            }

            using Stream? s = asm.GetManifestResourceStream(resName);
            if (s == null)
            {
                return new Dictionary<string, string>();
            }

            var result = new Dictionary<string, string>();
            using JsonDocument doc = JsonDocument.Parse(s);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Gets a string by resw key or by UI alias; returns the key itself when nothing is found.</summary>
    public string Get(string key)
    {
        key = key.Trim();
        string real = s_alias.TryGetValue(key, out string? a) ? a : key;
        if (_current.TryGetValue(real, out string? v) && !string.IsNullOrEmpty(v))
        {
            return Clean(v);
        }

        if (_fallback.TryGetValue(real, out v) && !string.IsNullOrEmpty(v))
        {
            return Clean(v);
        }

        string? own = AppStrings.Get(Locale, real);
        if (own != null)
        {
            return own;
        }

        if (s_hardcoded.TryGetValue(key, out v))
        {
            return v;
        }

        return key;
    }

    /// <summary>Gets a string and replaces its {0}, {1}... placeholders.</summary>
    public string Format(string key, params object[] args) => string.Format(_culture, Get(key), args);

    /// <summary>Returns the string, or <paramref name="defaultValue"/> when the key is unknown.</summary>
    public string GetOr(string key, string defaultValue)
    {
        string v = Get(key);
        return v == key.Trim() ? defaultValue : v;
    }

    private static string Clean(string v) => v.Replace("&amp;", "&");

    // ---- IResourceProvider ----

    public string GetCEngineString(string id)
    {
        switch (id)
        {
            case "sDecimal":
            {
                string sep = _culture.NumberFormat.NumberDecimalSeparator;
                return string.IsNullOrEmpty(sep) ? "." : sep.Substring(0, 1);
            }

            case "sThousand":
            {
                string sep = _culture.NumberFormat.NumberGroupSeparator;
                return string.IsNullOrEmpty(sep) ? "," : sep.Substring(0, 1);
            }

            case "sGrouping":
            {
                int[] sizes = _culture.NumberFormat.NumberGroupSizes;
                if (sizes == null || sizes.Length == 0 || sizes[0] == 0)
                {
                    return "3;0";
                }

                var parts = sizes.Where(x => x > 0).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                parts.Add("0");
                return string.Join(";", parts);
            }
        }

        if (_current.TryGetValue(id, out string? v) && !string.IsNullOrEmpty(v))
        {
            return v;
        }

        if (_fallback.TryGetValue(id, out v) && !string.IsNullOrEmpty(v))
        {
            return v;
        }

        return string.Empty;
    }
}
