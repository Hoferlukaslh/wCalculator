using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace CalcApp.Services;

/// <summary>
/// Applies the appearance settings (theme, accent colour, transparency) at run time.
/// The colours of the theme are written as brushes into <c>Application.Resources</c>, which takes precedence over the
/// default brushes of Styles/Calc.axaml, so every <c>DynamicResource</c> of the interface is updated at once.
/// </summary>
public static class AppearanceManager
{
    private sealed class Palette
    {
        public Color AppBg, PanelBg, PanelBorder, Fg, SubFg, ErrorFg;
        public Color DigitBg, DigitHover, DigitPressed, OpBg, OpHover, OpPressed;
        public Color Accent;
        public bool Dark;
    }

    // Same values as the ThemeDictionaries of Styles/Calc.axaml (those are the fallback used before the first Apply).
    private static readonly Palette s_light = new Palette
    {
        AppBg = Color.Parse("#F3F3F3"), PanelBg = Color.Parse("#FBFBFB"), PanelBorder = Color.Parse("#D6D6D6"),
        Fg = Color.Parse("#1A1A1A"), SubFg = Color.Parse("#605E5C"), ErrorFg = Color.Parse("#C42B1C"),
        DigitBg = Color.Parse("#FFFFFF"), DigitHover = Color.Parse("#F6F6F6"), DigitPressed = Color.Parse("#E4E4E4"),
        OpBg = Color.Parse("#EDEDED"), OpHover = Color.Parse("#E1E1E1"), OpPressed = Color.Parse("#D2D2D2"),
        Accent = Color.Parse("#0067C0"), Dark = false,
    };

    private static readonly Palette s_dark = new Palette
    {
        AppBg = Color.Parse("#202020"), PanelBg = Color.Parse("#2B2B2B"), PanelBorder = Color.Parse("#404040"),
        Fg = Color.Parse("#FFFFFF"), SubFg = Color.Parse("#A8A8A8"), ErrorFg = Color.Parse("#FF99A4"),
        DigitBg = Color.Parse("#3B3B3B"), DigitHover = Color.Parse("#343434"), DigitPressed = Color.Parse("#2A2A2A"),
        OpBg = Color.Parse("#323232"), OpHover = Color.Parse("#3B3B3B"), OpPressed = Color.Parse("#272727"),
        Accent = Color.Parse("#4CC2FF"), Dark = true,
    };

    private static Application? s_app;
    private static bool s_transparencySupported;

    /// <summary>The accent colours offered by the settings page.</summary>
    public static readonly string[] AccentPalette =
    {
        "#0067C0", "#0F7B0F", "#C42B1C", "#E3008C", "#8764B8", "#038387",
        "#CA5010", "#767676", "#FFB900", "#4CC2FF", "#10893E", "#744DA9",
    };

    /// <summary>True when the platform has windows that can be see-through (desktop). False on Android.</summary>
    public static bool TransparencyAvailable { get; set; }

    /// <summary>True when the window system really gave the window a transparent / blurred surface.</summary>
    public static bool TransparencySupported
    {
        get => s_transparencySupported;
        set
        {
            if (s_transparencySupported != value)
            {
                s_transparencySupported = value;
                ApplyResources();
            }
        }
    }

    /// <summary>Raised after the resources were rewritten.</summary>
    public static event Action? Changed;

    /// <summary>Applies the saved settings. Call once at start-up, then after every change.</summary>
    public static void Apply(Application app)
    {
        if (s_app == null)
        {
            s_app = app;
            app.ActualThemeVariantChanged += (_, _) => ApplyResources();
        }

        AppSettings s = SettingsService.Instance.Current;
        app.RequestedThemeVariant = s.Theme switch
        {
            ThemeChoice.Dark => ThemeVariant.Dark,
            ThemeChoice.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };

        ApplyResources();
    }

    /// <summary>Parses "#RRGGBB" (or "RRGGBB"); alpha is not accepted.</summary>
    public static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string t = text.Trim();
        if (t.StartsWith("#", StringComparison.Ordinal))
        {
            t = t.Substring(1);
        }

        if (t.Length != 6 || !uint.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb))
        {
            return false;
        }

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    public static string ToHex(Color c) => string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

    private static void ApplyResources()
    {
        Application? app = s_app;
        if (app == null)
        {
            return;
        }

        AppSettings s = SettingsService.Instance.Current;
        Palette p = app.ActualThemeVariant == ThemeVariant.Dark ? s_dark : s_light;

        double t = TransparencyAvailable && s_transparencySupported ? Math.Clamp(s.Transparency, 0, 100) / 100.0 : 0.0;
        byte appAlpha = Alpha(1.0 - 0.85 * t);
        byte panelAlpha = Alpha(1.0 - 0.65 * t);
        byte keyAlpha = Alpha(1.0 - 0.45 * t);

        Color accent = TryParseColor(s.AccentColor, out Color custom) ? custom : p.Accent;
        Color accentHover = p.Dark ? Mix(accent, Colors.Black, 0.06) : Mix(accent, Colors.White, 0.10);
        Color accentPressed = p.Dark ? Mix(accent, Colors.Black, 0.12) : Mix(accent, Colors.White, 0.20);
        Color accentFg = Luminance(accent) > 0.6 ? Colors.Black : Colors.White;

        var values = new Dictionary<string, Color>
        {
            ["AppBg"] = WithAlpha(p.AppBg, appAlpha),
            ["PanelBg"] = WithAlpha(p.PanelBg, panelAlpha),
            ["AppBgSolid"] = p.AppBg,
            ["MenuBg"] = WithAlpha(p.PanelBg, Alpha(1.0 - 0.8 * (Math.Clamp(s.MenuTransparency, 0, 100) / 100.0))),
            ["OverlayBg"] = WithAlpha(p.PanelBg, Alpha(1.0 - 0.15 * t)),
            ["PanelBorder"] = p.PanelBorder,
            ["Fg"] = p.Fg,
            ["SubFg"] = p.SubFg,
            ["ErrorFg"] = p.ErrorFg,
            ["DigitBg"] = WithAlpha(p.DigitBg, keyAlpha),
            ["DigitHover"] = WithAlpha(p.DigitHover, keyAlpha),
            ["DigitPressed"] = WithAlpha(p.DigitPressed, keyAlpha),
            ["OpBg"] = WithAlpha(p.OpBg, keyAlpha),
            ["OpHover"] = WithAlpha(p.OpHover, keyAlpha),
            ["OpPressed"] = WithAlpha(p.OpPressed, keyAlpha),
            ["AccentBg"] = accent,
            ["AccentHover"] = accentHover,
            ["AccentPressed"] = accentPressed,
            ["AccentFg"] = accentFg,
        };

        foreach (KeyValuePair<string, Color> kv in values)
        {
            app.Resources[kv.Key] = new SolidColorBrush(kv.Value);
        }

        // The standard controls (radio buttons, sliders, text box focus, ...) take their accent from the Fluent theme,
        // which by default is the accent colour of the operating system: give them the same colour as the keys.
        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorLight1"] = Mix(accent, Colors.White, 0.15);
        app.Resources["SystemAccentColorLight2"] = Mix(accent, Colors.White, 0.30);
        app.Resources["SystemAccentColorLight3"] = Mix(accent, Colors.White, 0.45);
        app.Resources["SystemAccentColorDark1"] = Mix(accent, Colors.Black, 0.15);
        app.Resources["SystemAccentColorDark2"] = Mix(accent, Colors.Black, 0.30);
        app.Resources["SystemAccentColorDark3"] = Mix(accent, Colors.Black, 0.45);

        Changed?.Invoke();
    }

    private static byte Alpha(double a) => (byte)Math.Round(Math.Clamp(a, 0.0, 1.0) * 255.0);

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + ((b.R - a.R) * t)),
        (byte)Math.Round(a.G + ((b.G - a.G) * t)),
        (byte)Math.Round(a.B + ((b.B - a.B) * t)));

    private static double Luminance(Color c) => ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) / 255.0;
}
