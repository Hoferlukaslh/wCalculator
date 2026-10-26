using System;
using Avalonia;
using Avalonia.Controls;
using CalcApp.Services;

namespace CalcApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => UpdateTransparencySupport();
        AttachedToVisualTree += (_, _) => AppearanceManager.Changed += ApplyEffect;
        DetachedFromVisualTree += (_, _) => AppearanceManager.Changed -= ApplyEffect;
        ApplyEffect();
    }

    /// <summary>
    /// Chooses what the window system draws behind the window. The strength of the blur is decided by the window
    /// system (KDE: desktop effect settings, Windows: acrylic), applications cannot change it.
    /// </summary>
    private void ApplyEffect()
    {
        TransparencyLevelHint = SettingsService.Instance.Current.Effect switch
        {
            WindowEffect.None => new[] { WindowTransparencyLevel.Transparent },
            WindowEffect.Blur => new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent },
            _ => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent },
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActualTransparencyLevelProperty)
        {
            UpdateTransparencySupport();
        }
    }

    /// <summary>
    /// The window always asks for a blurred / transparent surface (the interface paints itself opaque when the
    /// transparency setting is 0). The settings page is only usable when the window system granted one.
    /// </summary>
    private void UpdateTransparencySupport()
    {
        AppearanceManager.TransparencySupported = ActualTransparencyLevel != WindowTransparencyLevel.None;
    }
}
