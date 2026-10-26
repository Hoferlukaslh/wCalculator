using System;
using Avalonia;
using CalcApp;

namespace Calculator.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<CalcApp.App>()
            .UsePlatformDetect()
            .LogToTrace();
}
