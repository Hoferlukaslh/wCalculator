using System;
using System.Threading.Tasks;
using CalcApp.ViewModels;

namespace CalcApp.Services;

/// <summary>
/// Builds the main view model (settings file, translations, calculation engine) on a second thread while the main
/// thread is busy setting up Avalonia and the window system, which takes about 150 ms and needs none of it.
/// <see cref="Begin"/> is called as early as possible (first line of <c>Main</c>); <see cref="TakeViewModel"/> hands
/// the result to the UI thread when the application is ready to build its window.
/// Nothing in the view models touches the UI thread (no dispatcher, no control), so building them elsewhere is safe;
/// the hand-over through the task makes the result visible to the UI thread.
/// </summary>
public static class StartupWarmup
{
    private static readonly object s_lock = new object();
    private static Task<MainViewModel>? s_task;

    /// <summary>Starts building the main view model in the background. Calling it again does nothing.</summary>
    public static void Begin()
    {
        lock (s_lock)
        {
            s_task ??= Task.Run(Build);
        }
    }

    /// <summary>The main view model: waits for the background work if it is not finished, or builds it here if that failed.</summary>
    public static MainViewModel TakeViewModel()
    {
        Begin();
        try
        {
            return s_task!.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Build it again here: a real problem then surfaces with its normal stack trace.
            return new MainViewModel();
        }
    }

    private static MainViewModel Build()
    {
        // The settings are read first: the UI thread needs them right after the framework is ready.
        _ = SettingsService.Instance.Current;
        MainViewModel viewModel = new MainViewModel();
        StartupTrace.Mark("warmup-done");
        return viewModel;
    }
}
