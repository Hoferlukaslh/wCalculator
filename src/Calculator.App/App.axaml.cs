using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CalcApp.Services;
using CalcApp.ViewModels;
using CalcApp.Views;

namespace CalcApp;

public partial class App : Application
{
    public override void Initialize()
    {
        StartupTrace.Mark("app-initialize-start"); // main -> here: Avalonia and the platform (window system, Skia, fonts)
        AvaloniaXamlLoader.Load(this);
        StartupTrace.Mark("app-xaml-loaded");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Windows can be see-through on the desktop; mobile has a single full-screen view.
        AppearanceManager.TransparencyAvailable = ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
        AppearanceManager.Apply(this);
        StartupTrace.Mark("appearance-applied");

        // Built in the background since the start of the process (see StartupWarmup): normally ready by now.
        MainViewModel viewModel = StartupWarmup.TakeViewModel();
        StartupTrace.Mark("viewmodel-created");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = viewModel };
        }

        StartupTrace.Mark("window-constructed");

        base.OnFrameworkInitializationCompleted();
        StartupTrace.Mark("framework-ready");
    }
}
