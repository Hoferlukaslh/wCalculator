using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CalcApp.Localization;
using CalcApp.Services;

namespace CalcApp.ViewModels;

/// <summary>A colour of the accent palette.</summary>
public sealed class AccentSwatch : ViewModelBase
{
    private bool _isSelected;

    public AccentSwatch(string hex, IBrush brush)
    {
        Hex = hex;
        Brush = brush;
    }

    /// <summary>"#RRGGBB", or an empty text for the default accent of the theme.</summary>
    public string Hex { get; }

    public IBrush Brush { get; }

    public bool IsDefault => Hex.Length == 0;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ShowCheck));
            }
        }
    }

    /// <summary>The check mark is drawn on the selected colours (the default swatch shows its own icon).</summary>
    public bool ShowCheck => _isSelected && !IsDefault;
}

/// <summary>View model of the settings page: theme, accent colour and transparency.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _service = SettingsService.Instance;
    private readonly Loc _loc = Loc.Instance;
    private readonly DispatcherTimer _saveTimer;
    private string _customAccent = string.Empty;
    private bool _updatingCustom;

    public SettingsViewModel()
    {
        _startEffect = _service.Current.Effect;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _service.Save();
        };

        Swatches.Add(new AccentSwatch(string.Empty, Brushes.Transparent));
        foreach (string hex in AppearanceManager.AccentPalette)
        {
            AppearanceManager.TryParseColor(hex, out Color c);
            Swatches.Add(new AccentSwatch(hex, new SolidColorBrush(c)));
        }

        SelectAccentCommand = new RelayCommand(p =>
        {
            if (p is AccentSwatch swatch)
            {
                SetAccent(swatch.Hex);
            }
        });
        ResetCommand = new RelayCommand(Reset);

        RefreshAccentSelection();
        AppearanceManager.Changed += OnAppearanceChanged;
    }

    public ObservableCollection<AccentSwatch> Swatches { get; } = new ObservableCollection<AccentSwatch>();

    public ICommand SelectAccentCommand { get; }

    public ICommand ResetCommand { get; }

    // ------------------------------------------------------------------ theme

    public bool IsThemeDevice
    {
        get => _service.Current.Theme == ThemeChoice.Device;
        set => SetTheme(value, ThemeChoice.Device);
    }

    public bool IsThemeDark
    {
        get => _service.Current.Theme == ThemeChoice.Dark;
        set => SetTheme(value, ThemeChoice.Dark);
    }

    public bool IsThemeLight
    {
        get => _service.Current.Theme == ThemeChoice.Light;
        set => SetTheme(value, ThemeChoice.Light);
    }

    private void SetTheme(bool isChecked, ThemeChoice theme)
    {
        // A radio button reports "false" for the item that loses the selection: only the new selection counts.
        if (!isChecked || _service.Current.Theme == theme)
        {
            return;
        }

        _service.Current.Theme = theme;
        OnPropertyChanged(nameof(IsThemeDevice));
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(IsThemeLight));
        ApplyAndSave();
    }

    // ------------------------------------------------------------------ accent

    /// <summary>Text field for a custom colour ("#RRGGBB").</summary>
    public string CustomAccent
    {
        get => _customAccent;
        set
        {
            if (!SetProperty(ref _customAccent, value ?? string.Empty) || _updatingCustom)
            {
                return;
            }

            if (AppearanceManager.TryParseColor(_customAccent, out Color c))
            {
                SetAccent(AppearanceManager.ToHex(c));
            }
        }
    }

    private void SetAccent(string hex)
    {
        _service.Current.AccentColor = hex;
        RefreshAccentSelection();
        ApplyAndSave();
    }

    private void RefreshAccentSelection()
    {
        string current = _service.Current.AccentColor;
        foreach (AccentSwatch s in Swatches)
        {
            s.IsSelected = string.Equals(s.Hex, current, StringComparison.OrdinalIgnoreCase);
        }

        _updatingCustom = true;
        try
        {
            if (!string.Equals(_customAccent, current, StringComparison.OrdinalIgnoreCase)
                && !(AppearanceManager.TryParseColor(_customAccent, out Color typed) && AppearanceManager.ToHex(typed) == current))
            {
                CustomAccent = current;
            }
        }
        finally
        {
            _updatingCustom = false;
        }
    }

    // ------------------------------------------------------------------ transparency

    /// <summary>Transparency in percent (0 = opaque).</summary>
    public double Transparency
    {
        get => _service.Current.Transparency;
        set
        {
            int percent = (int)Math.Round(Math.Clamp(value, 0, 100));
            if (percent == _service.Current.Transparency)
            {
                return;
            }

            _service.Current.Transparency = percent;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TransparencyText));
            ApplyAndSave();
        }
    }

    public bool IsEffectNone
    {
        get => _service.Current.Effect == WindowEffect.None;
        set => SetEffect(value, WindowEffect.None);
    }

    public bool IsEffectBlur
    {
        get => _service.Current.Effect == WindowEffect.Blur;
        set => SetEffect(value, WindowEffect.Blur);
    }

    public bool IsEffectAcrylic
    {
        get => _service.Current.Effect == WindowEffect.Acrylic;
        set => SetEffect(value, WindowEffect.Acrylic);
    }

    private void SetEffect(bool isChecked, WindowEffect effect)
    {
        if (!isChecked || _service.Current.Effect == effect)
        {
            return;
        }

        _service.Current.Effect = effect;
        OnPropertyChanged(nameof(IsEffectNone));
        OnPropertyChanged(nameof(IsEffectBlur));
        OnPropertyChanged(nameof(IsEffectAcrylic));
        OnPropertyChanged(nameof(EffectNeedsRestart));
        ApplyAndSave();
    }

    public string TransparencyText => _service.Current.Transparency + " %";

    /// <summary>True when the slider can be used: the platform has real windows and the window system supports transparency.</summary>
    public bool IsTransparencyEnabled => AppearanceManager.TransparencyAvailable && AppearanceManager.TransparencySupported;

    /// <summary>Short explanation under the transparency slider (the whole card is hidden where it does not apply).</summary>
    public string TransparencyNote => _loc.Get("TransparencyHint");

    private readonly WindowEffect _startEffect;

    /// <summary>
    /// Some window systems only read the blur request when the window is created: tell the user to restart the
    /// application after the effect was changed.
    /// </summary>
    public bool EffectNeedsRestart => _service.Current.Effect != _startEffect;

    // ------------------------------------------------------------------ navigation menu

    /// <summary>Transparency of the navigation menu over the page, in percent.</summary>
    public double MenuTransparency
    {
        get => _service.Current.MenuTransparency;
        set
        {
            int percent = (int)Math.Round(Math.Clamp(value, 0, 100));
            if (percent == _service.Current.MenuTransparency)
            {
                return;
            }

            _service.Current.MenuTransparency = percent;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MenuTransparencyText));
            ApplyAndSave();
        }
    }

    public string MenuTransparencyText => _service.Current.MenuTransparency + " %";

    /// <summary>Blur radius of the page behind the navigation menu (0 = no blur).</summary>
    public double MenuBlur
    {
        get => _service.Current.MenuBlur;
        set
        {
            int radius = (int)Math.Round(Math.Clamp(value, 0, 30));
            if (radius == _service.Current.MenuBlur)
            {
                return;
            }

            _service.Current.MenuBlur = radius;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MenuBlurText));
            ApplyAndSave();
        }
    }

    public string MenuBlurText => _service.Current.MenuBlur.ToString();

    // ------------------------------------------------------------------ haptic feedback (mobile only)

    /// <summary>True when the device has a vibration motor: the whole card is hidden otherwise.</summary>
    public bool IsHapticsAvailable => Haptics.IsAvailable;

    /// <summary>True when the device cannot set the strength of the vibration (a note is shown then).</summary>
    public bool HapticsNoAmplitude => Haptics.IsAvailable && !Haptics.HasAmplitudeControl;

    public bool HapticsEnabled
    {
        get => _service.Current.HapticsEnabled;
        set
        {
            if (_service.Current.HapticsEnabled == value)
            {
                return;
            }

            _service.Current.HapticsEnabled = value;
            OnPropertyChanged();
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    /// <summary>Length of the vibration in milliseconds.</summary>
    public double HapticsDuration
    {
        get => _service.Current.HapticsDuration;
        set
        {
            int ms = (int)Math.Round(Math.Clamp(value, Haptics.MinDuration, Haptics.MaxDuration));
            if (ms == _service.Current.HapticsDuration)
            {
                return;
            }

            _service.Current.HapticsDuration = ms;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HapticsDurationText));
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    public string HapticsDurationText => _service.Current.HapticsDuration + " ms";

    /// <summary>Strength of the vibration in percent.</summary>
    public double HapticsIntensity
    {
        get => _service.Current.HapticsIntensity;
        set
        {
            int percent = (int)Math.Round(Math.Clamp(value, Haptics.MinIntensity, Haptics.MaxIntensity));
            if (percent == _service.Current.HapticsIntensity)
            {
                return;
            }

            _service.Current.HapticsIntensity = percent;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HapticsIntensityText));
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    public string HapticsIntensityText => _service.Current.HapticsIntensity + " %";

    private void OnAppearanceChanged()
    {
        OnPropertyChanged(nameof(IsTransparencyEnabled));
        OnPropertyChanged(nameof(TransparencyNote));
    }

    // ------------------------------------------------------------------ common

    private void Reset()
    {
        AppSettings s = _service.Current;
        s.Theme = ThemeChoice.Device;
        s.AccentColor = string.Empty;
        s.Transparency = 0;
        s.Effect = WindowEffect.Acrylic;
        OnPropertyChanged(nameof(EffectNeedsRestart));
        OnPropertyChanged(nameof(IsEffectNone));
        OnPropertyChanged(nameof(IsEffectBlur));
        OnPropertyChanged(nameof(IsEffectAcrylic));
        s.MenuTransparency = AppSettings.DefaultMenuTransparency;
        s.MenuBlur = AppSettings.DefaultMenuBlur;
        s.HapticsEnabled = true;
        s.HapticsDuration = AppSettings.DefaultHapticsDuration;
        s.HapticsIntensity = AppSettings.DefaultHapticsIntensity;
        OnPropertyChanged(nameof(HapticsEnabled));
        OnPropertyChanged(nameof(HapticsDuration));
        OnPropertyChanged(nameof(HapticsDurationText));
        OnPropertyChanged(nameof(HapticsIntensity));
        OnPropertyChanged(nameof(HapticsIntensityText));
        OnPropertyChanged(nameof(MenuTransparency));
        OnPropertyChanged(nameof(MenuTransparencyText));
        OnPropertyChanged(nameof(MenuBlur));
        OnPropertyChanged(nameof(MenuBlurText));
        OnPropertyChanged(nameof(IsThemeDevice));
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(Transparency));
        OnPropertyChanged(nameof(TransparencyText));
        RefreshAccentSelection();
        ApplyAndSave();
    }

    private void ApplyAndSave()
    {
        if (Application.Current != null)
        {
            AppearanceManager.Apply(Application.Current);
        }

        // Saved a moment after the last change so that dragging the slider does not write the file continuously.
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
