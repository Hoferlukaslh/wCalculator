using System;

namespace CalcApp.Services;

/// <summary>
/// Startup timing for bench-startup.sh. Does nothing unless WCALC_STARTUP_TRACE=1: each milestone is then written to
/// the standard error as  <c>WCALC_STARTUP &lt;name&gt; &lt;microseconds since 1970-01-01 UTC&gt;</c>, so that the script can subtract
/// the moment it launched the process. With WCALC_EXIT_AFTER_STARTUP=1 the application closes itself once the window
/// is shown and idle.
/// </summary>
public static class StartupTrace
{
    private static readonly bool s_enabled = Environment.GetEnvironmentVariable("WCALC_STARTUP_TRACE") == "1";

    /// <summary>True when the application must close itself after its first idle moment.</summary>
    public static bool ExitAfterStartup { get; } = s_enabled && Environment.GetEnvironmentVariable("WCALC_EXIT_AFTER_STARTUP") == "1";

    public static void Mark(string milestone)
    {
        if (!s_enabled)
        {
            return;
        }

        long microseconds = (DateTime.UtcNow - DateTime.UnixEpoch).Ticks / 10;
        Console.Error.WriteLine("WCALC_STARTUP " + milestone + " " + microseconds);
    }
}
