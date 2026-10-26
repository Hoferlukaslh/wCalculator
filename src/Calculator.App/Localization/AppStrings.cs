using System.Collections.Generic;

namespace CalcApp.Localization;

/// <summary>
/// Strings that do not exist in the original Windows Calculator resources (settings page, currency status).
/// English is the fallback; French, German and Spanish are provided.
/// </summary>
internal static class AppStrings
{
    private static readonly Dictionary<string, Dictionary<string, string>> s_tables = new Dictionary<string, Dictionary<string, string>>
    {
        ["en"] = new Dictionary<string, string>
        {
            ["MenuSection"] = "Navigation menu",
            ["MenuTransparency"] = "Menu transparency",
            ["MenuBlur"] = "Menu blur",
            ["WindowEffect"] = "Window effect",
            ["WindowEffectNone"] = "See-through only",
            ["WindowEffectBlur"] = "Blur",
            ["WindowEffectAcrylic"] = "Acrylic",
            ["WindowEffectNote"] = "The strength of the blur is set by the system (e.g. KDE desktop effects settings), not by the application.",
            ["RestartNeeded"] = "Restart the application to apply this change.",
            ["Theme"] = "Theme",
            ["ThemeDevice"] = "Use device setting",
            ["ThemeDark"] = "Dark",
            ["ThemeLight"] = "Light",
            ["AccentColor"] = "Accent colour",
            ["AccentDefault"] = "Default",
            ["AccentCustom"] = "Custom colour (#RRGGBB)",
            ["Transparency"] = "Transparency",
            ["TransparencyHint"] = "Frosted-glass effect behind the window.",
            ["TransparencyUnavailable"] = "Not supported by this system.",
            ["TransparencyNotOnMobile"] = "Not available on mobile devices.",
            ["ResetSettings"] = "Reset appearance",
            ["HapticsSection"] = "Haptic feedback",
            ["HapticsEnabled"] = "Vibrate when a key is pressed",
            ["HapticsDuration"] = "Duration",
            ["HapticsIntensity"] = "Intensity",
            ["HapticsNoAmplitude"] = "This device cannot adjust the intensity: only the duration is used.",
            ["HapticsTest"] = "Try it",
            ["CurrencyUpdated"] = "Rates of {0}",
            ["CurrencyRefreshing"] = "Updating rates…",
            ["CurrencyOffline"] = "Offline – rates of {0}",
            ["CurrencyNoRates"] = "No rates available. Check your Internet connection, then refresh.",
            ["CurrencyRefresh"] = "Refresh rates",
            ["CurrencySource"] = "Source: currency-api by fawazahmed0 (CC0)",
            ["IconsCredit"] = "Icons: Phosphor Icons (MIT)",
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["MenuSection"] = "Menu de navigation",
            ["MenuTransparency"] = "Transparence du menu",
            ["MenuBlur"] = "Flou du menu",
            ["WindowEffect"] = "Effet de fenêtre",
            ["WindowEffectNone"] = "Transparent seulement",
            ["WindowEffectBlur"] = "Flou",
            ["WindowEffectAcrylic"] = "Acrylique",
            ["WindowEffectNote"] = "L’intensité du flou est fixée par le système (par ex. réglages des effets de bureau de KDE), pas par l’application.",
            ["RestartNeeded"] = "Redémarre l’application pour appliquer ce changement.",
            ["Theme"] = "Thème",
            ["ThemeDevice"] = "Utiliser le réglage de l’appareil",
            ["ThemeDark"] = "Sombre",
            ["ThemeLight"] = "Clair",
            ["AccentColor"] = "Couleur d’accent",
            ["AccentDefault"] = "Par défaut",
            ["AccentCustom"] = "Couleur personnalisée (#RRGGBB)",
            ["Transparency"] = "Transparence",
            ["TransparencyHint"] = "Effet de verre dépoli derrière la fenêtre.",
            ["TransparencyUnavailable"] = "Non pris en charge par ce système.",
            ["TransparencyNotOnMobile"] = "Indisponible sur mobile.",
            ["ResetSettings"] = "Réinitialiser l’apparence",
            ["HapticsSection"] = "Retour haptique",
            ["HapticsEnabled"] = "Vibrer à l’appui sur une touche",
            ["HapticsDuration"] = "Durée",
            ["HapticsIntensity"] = "Intensité",
            ["HapticsNoAmplitude"] = "Cet appareil ne peut pas régler l’intensité : seule la durée est utilisée.",
            ["HapticsTest"] = "Tester",
            ["CurrencyUpdated"] = "Taux du {0}",
            ["CurrencyRefreshing"] = "Mise à jour des taux…",
            ["CurrencyOffline"] = "Hors ligne – taux du {0}",
            ["CurrencyNoRates"] = "Aucun taux disponible. Vérifiez votre connexion Internet, puis actualisez.",
            ["CurrencyRefresh"] = "Actualiser les taux",
            ["CurrencySource"] = "Source : currency-api de fawazahmed0 (CC0)",
            ["IconsCredit"] = "Icônes : Phosphor Icons (MIT)",
        },
        ["de"] = new Dictionary<string, string>
        {
            ["MenuSection"] = "Navigationsmenü",
            ["MenuTransparency"] = "Menütransparenz",
            ["MenuBlur"] = "Menüunschärfe",
            ["WindowEffect"] = "Fenstereffekt",
            ["WindowEffectNone"] = "Nur durchsichtig",
            ["WindowEffectBlur"] = "Weichzeichner",
            ["WindowEffectAcrylic"] = "Acryl",
            ["WindowEffectNote"] = "Die Stärke der Unschärfe legt das System fest (z. B. KDE-Desktopeffekte), nicht die Anwendung.",
            ["RestartNeeded"] = "Starten Sie die Anwendung neu, um die Änderung zu übernehmen.",
            ["Theme"] = "Design",
            ["ThemeDevice"] = "Geräteeinstellung verwenden",
            ["ThemeDark"] = "Dunkel",
            ["ThemeLight"] = "Hell",
            ["AccentColor"] = "Akzentfarbe",
            ["AccentDefault"] = "Standard",
            ["AccentCustom"] = "Eigene Farbe (#RRGGBB)",
            ["Transparency"] = "Transparenz",
            ["TransparencyHint"] = "Milchglas-Effekt hinter dem Fenster.",
            ["TransparencyUnavailable"] = "Von diesem System nicht unterstützt.",
            ["TransparencyNotOnMobile"] = "Auf Mobilgeräten nicht verfügbar.",
            ["ResetSettings"] = "Darstellung zurücksetzen",
            ["HapticsSection"] = "Haptisches Feedback",
            ["HapticsEnabled"] = "Bei Tastendruck vibrieren",
            ["HapticsDuration"] = "Dauer",
            ["HapticsIntensity"] = "Intensität",
            ["HapticsNoAmplitude"] = "Dieses Gerät kann die Intensität nicht regeln: nur die Dauer wird verwendet.",
            ["HapticsTest"] = "Testen",
            ["CurrencyUpdated"] = "Kurse vom {0}",
            ["CurrencyRefreshing"] = "Kurse werden aktualisiert…",
            ["CurrencyOffline"] = "Offline – Kurse vom {0}",
            ["CurrencyNoRates"] = "Keine Kurse verfügbar. Internetverbindung prüfen und aktualisieren.",
            ["CurrencyRefresh"] = "Kurse aktualisieren",
            ["CurrencySource"] = "Quelle: currency-api von fawazahmed0 (CC0)",
            ["IconsCredit"] = "Symbole: Phosphor Icons (MIT)",
        },
        ["es"] = new Dictionary<string, string>
        {
            ["MenuSection"] = "Menú de navegación",
            ["MenuTransparency"] = "Transparencia del menú",
            ["MenuBlur"] = "Desenfoque del menú",
            ["WindowEffect"] = "Efecto de ventana",
            ["WindowEffectNone"] = "Solo transparente",
            ["WindowEffectBlur"] = "Desenfoque",
            ["WindowEffectAcrylic"] = "Acrílico",
            ["WindowEffectNote"] = "La intensidad del desenfoque la define el sistema (p. ej. efectos de escritorio de KDE), no la aplicación.",
            ["RestartNeeded"] = "Reinicie la aplicación para aplicar el cambio.",
            ["Theme"] = "Tema",
            ["ThemeDevice"] = "Usar la configuración del dispositivo",
            ["ThemeDark"] = "Oscuro",
            ["ThemeLight"] = "Claro",
            ["AccentColor"] = "Color de énfasis",
            ["AccentDefault"] = "Predeterminado",
            ["AccentCustom"] = "Color personalizado (#RRGGBB)",
            ["Transparency"] = "Transparencia",
            ["TransparencyHint"] = "Efecto de vidrio esmerilado detrás de la ventana.",
            ["TransparencyUnavailable"] = "No compatible con este sistema.",
            ["TransparencyNotOnMobile"] = "No disponible en dispositivos móviles.",
            ["ResetSettings"] = "Restablecer apariencia",
            ["HapticsSection"] = "Respuesta háptica",
            ["HapticsEnabled"] = "Vibrar al pulsar una tecla",
            ["HapticsDuration"] = "Duración",
            ["HapticsIntensity"] = "Intensidad",
            ["HapticsNoAmplitude"] = "Este dispositivo no puede regular la intensidad: solo se usa la duración.",
            ["HapticsTest"] = "Probar",
            ["CurrencyUpdated"] = "Tipos del {0}",
            ["CurrencyRefreshing"] = "Actualizando tipos…",
            ["CurrencyOffline"] = "Sin conexión – tipos del {0}",
            ["CurrencyNoRates"] = "No hay tipos disponibles. Compruebe su conexión a Internet y actualice.",
            ["CurrencyRefresh"] = "Actualizar tipos",
            ["CurrencySource"] = "Fuente: currency-api de fawazahmed0 (CC0)",
            ["IconsCredit"] = "Iconos: Phosphor Icons (MIT)",
        },
    };

    /// <summary>Returns the string for a locale name such as "fr-FR", or null when the key is unknown.</summary>
    public static string? Get(string locale, string key)
    {
        string lang = locale.Split('-')[0].ToLowerInvariant();
        if (s_tables.TryGetValue(lang, out Dictionary<string, string>? table) && table.TryGetValue(key, out string? v))
        {
            return v;
        }

        return s_tables["en"].TryGetValue(key, out v) ? v : null;
    }
}
