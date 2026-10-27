using System;
using Avalonia;
using CalcApp;
using CalcApp.Services;

namespace Calculator.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTrace.Mark("main");

        // The settings, the translations and the calculation engine are prepared on a second thread while Avalonia sets
        // up the window system (about 150 ms of the main thread, which does not need them).
        StartupWarmup.Begin();

        AppBuilder builder = BuildAvaloniaApp();
        StartupTrace.Mark("builder-ready");
        builder.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<CalcApp.App>()
            .UsePlatformDetect()
            .LogToTrace()
            .AfterPlatformServicesSetup(_ => StartupTrace.Mark("platform-ready")); // window system and Skia are set up

        // Linux: draw on the CPU by default. A calculator needs no GPU, and not creating an OpenGL context shortens the
        // start-up (measured with bench-startup.sh: about 125 ms less, 605 -> 478 ms to a usable window).
        // WCALC_RENDERING=gpu (or "auto") gives Avalonia back the choice (OpenGL when available), for instance on a
        // machine where software drawing turns out to be slower.
        if (OperatingSystem.IsLinux() && !UsesGpuRendering())
        {
            builder = builder.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } });
        }

        return builder;
    }

    private static bool UsesGpuRendering()
    {
        string? mode = Environment.GetEnvironmentVariable("WCALC_RENDERING");
        return string.Equals(mode, "gpu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase);
    }
}
