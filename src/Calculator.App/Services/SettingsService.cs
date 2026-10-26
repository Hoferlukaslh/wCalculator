using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CalcApp.Services;

/// <summary>The blur the window system puts behind a see-through window.</summary>
public enum WindowEffect
{
    /// <summary>See-through only, no blur.</summary>
    None = 0,

    /// <summary>The blur of the window system (KDE, macOS, Windows).</summary>
    Blur = 1,

    /// <summary>Acrylic (Windows 10/11, falls back to blur, then to see-through only).</summary>
    Acrylic = 2,
}

public enum ThemeChoice
{
    /// <summary>Follow the theme of the device / operating system.</summary>
    Device = 0,
    Dark = 1,
    Light = 2,
}

/// <summary>The settings the user can change.</summary>
public sealed class AppSettings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.Device;

    /// <summary>Accent colour as "#RRGGBB"; empty for the default accent of the theme.</summary>
    public string AccentColor { get; set; } = string.Empty;

    /// <summary>How see-through the window is, 0 (opaque) to 100. Only used where the platform supports it.</summary>
    public int Transparency { get; set; }

    /// <summary>Which effect the window system applies behind the see-through window.</summary>
    public WindowEffect Effect { get; set; } = WindowEffect.Acrylic;

    /// <summary>How see-through the navigation menu is over the page, 0 to 100.</summary>
    public int MenuTransparency { get; set; } = DefaultMenuTransparency;

    /// <summary>Blur radius of the page behind the navigation menu, 0 (none) to 30.</summary>
    public int MenuBlur { get; set; } = DefaultMenuBlur;

    /// <summary>Vibrate when a key is touched (mobile only).</summary>
    public bool HapticsEnabled { get; set; } = true;

    /// <summary>Length of the vibration in milliseconds.</summary>
    public int HapticsDuration { get; set; } = DefaultHapticsDuration;

    /// <summary>Strength of the vibration in percent.</summary>
    public int HapticsIntensity { get; set; } = DefaultHapticsIntensity;

    public const int DefaultHapticsDuration = 12;

    public const int DefaultHapticsIntensity = 20;

    public const int DefaultMenuTransparency = 50;

    public const int DefaultMenuBlur = 30;
}

/// <summary>Loads and saves <see cref="AppSettings"/> as a small JSON file in the per-user data folder.</summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new SettingsService();

    private readonly string _path;

    /// <summary>Folder for the files of the application (settings, currency cache).</summary>
    public static string DataDirectory
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root))
            {
                root = Path.GetTempPath();
            }

            string dir = Path.Combine(root, "wCalculator");
            MigrateOldDirectory(root, dir);
            return dir;
        }
    }

    private static bool s_migrated;

    /// <summary>The application used to be called "CalculatorAvalonia": keep the settings and the currency cache of that folder.</summary>
    private static void MigrateOldDirectory(string root, string dir)
    {
        if (s_migrated)
        {
            return;
        }

        s_migrated = true;
        try
        {
            string old = Path.Combine(root, "CalculatorAvalonia");
            if (Directory.Exists(old) && !Directory.Exists(dir))
            {
                Directory.Move(old, dir);
            }
        }
        catch (Exception)
        {
            // The settings simply start from the defaults.
        }
    }

    private SettingsService()
    {
        _path = Path.Combine(DataDirectory, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; }

    private AppSettings Load()
    {
        var s = new AppSettings();
        try
        {
            if (!File.Exists(_path))
            {
                return s;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_path));
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return s;
            }

            if (root.TryGetProperty("theme", out JsonElement t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out int theme)
                && Enum.IsDefined(typeof(ThemeChoice), theme))
            {
                s.Theme = (ThemeChoice)theme;
            }

            if (root.TryGetProperty("accent", out JsonElement a) && a.ValueKind == JsonValueKind.String)
            {
                string accent = a.GetString() ?? string.Empty;
                s.AccentColor = AppearanceManager.TryParseColor(accent, out _) ? accent : string.Empty;
            }

            if (root.TryGetProperty("transparency", out JsonElement tr) && tr.ValueKind == JsonValueKind.Number && tr.TryGetInt32(out int percent))
            {
                s.Transparency = Math.Clamp(percent, 0, 100);
            }

            if (root.TryGetProperty("windowEffect", out JsonElement we) && we.ValueKind == JsonValueKind.Number && we.TryGetInt32(out int effect)
                && Enum.IsDefined(typeof(WindowEffect), effect))
            {
                s.Effect = (WindowEffect)effect;
            }

            if (root.TryGetProperty("menuTransparency", out JsonElement mt) && mt.ValueKind == JsonValueKind.Number && mt.TryGetInt32(out int menuT))
            {
                s.MenuTransparency = Math.Clamp(menuT, 0, 100);
            }

            if (root.TryGetProperty("menuBlur", out JsonElement mb) && mb.ValueKind == JsonValueKind.Number && mb.TryGetInt32(out int menuB))
            {
                s.MenuBlur = Math.Clamp(menuB, 0, 30);
            }

            if (root.TryGetProperty("haptics", out JsonElement he) && (he.ValueKind == JsonValueKind.True || he.ValueKind == JsonValueKind.False))
            {
                s.HapticsEnabled = he.GetBoolean();
            }

            if (root.TryGetProperty("hapticsDuration", out JsonElement hd) && hd.ValueKind == JsonValueKind.Number && hd.TryGetInt32(out int dur))
            {
                s.HapticsDuration = Math.Clamp(dur, Haptics.MinDuration, Haptics.MaxDuration);
            }

            if (root.TryGetProperty("hapticsIntensity", out JsonElement hi) && hi.ValueKind == JsonValueKind.Number && hi.TryGetInt32(out int inten))
            {
                s.HapticsIntensity = Math.Clamp(inten, Haptics.MinIntensity, Haptics.MaxIntensity);
            }
        }
        catch (Exception)
        {
            // A damaged settings file falls back to the defaults.
        }

        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("theme", (int)Current.Theme);
                w.WriteString("accent", Current.AccentColor);
                w.WriteNumber("transparency", Current.Transparency);
                w.WriteNumber("windowEffect", (int)Current.Effect);
                w.WriteNumber("menuTransparency", Current.MenuTransparency);
                w.WriteNumber("menuBlur", Current.MenuBlur);
                w.WriteBoolean("haptics", Current.HapticsEnabled);
                w.WriteNumber("hapticsDuration", Current.HapticsDuration);
                w.WriteNumber("hapticsIntensity", Current.HapticsIntensity);
                w.WriteEndObject();
            }

            File.WriteAllText(_path, Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception)
        {
            // Not being able to save the settings must never break the calculator.
        }
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}", Current.Theme, Current.AccentColor, Current.Transparency);
}
