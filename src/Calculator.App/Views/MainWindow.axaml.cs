using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CalcApp.Services;

namespace CalcApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        StartupTrace.Mark("window-base-created"); // the Window base class is built: the native window exists
        InitializeComponent();
        StartupTrace.Mark("window-xaml-loaded");
        Opened += (_, _) =>
        {
            UpdateTransparencySupport();
            StartupTrace.Mark("window-opened");

            // Background runs after the first layout and render passes: "first-idle" is when the window is really usable.
            Dispatcher.UIThread.Post(
                () =>
                {
                    StartupTrace.Mark("first-idle");
                    if (StartupTrace.ExitAfterStartup && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                },
                DispatcherPriority.Background);
        };
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
        WindowTransparencyLevel[] wanted = SettingsService.Instance.Current.Effect switch
        {
            WindowEffect.None => new[] { WindowTransparencyLevel.Transparent },
            WindowEffect.Blur => new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent },
            _ => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent },
        };

        // A new array counts as a change even with the same content, and the window system is then asked again:
        // only set the hint when the list differs from the current one.
        if (TransparencyLevelHint is { } current && current.SequenceEqual(wanted))
        {
            return;
        }

        TransparencyLevelHint = wanted;
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
