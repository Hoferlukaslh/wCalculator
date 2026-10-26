using System;

namespace CalcApp.Services;

/// <summary>The vibration motor of the device. Implemented by the Android head; there is none on the desktop.</summary>
public interface IHapticsDevice
{
    /// <summary>True when the device has a vibration motor.</summary>
    bool IsAvailable { get; }

    /// <summary>True when the strength of the vibration can be set (otherwise only the duration counts).</summary>
    bool HasAmplitudeControl { get; }

    /// <param name="durationMs">Length of the vibration in milliseconds.</param>
    /// <param name="amplitude">Strength from 1 (weakest) to 255 (strongest).</param>
    void Vibrate(int durationMs, int amplitude);
}

/// <summary>Vibration feedback when a key is touched (mobile only), set by the settings page.</summary>
public static class Haptics
{
    public const int MinDuration = 5;
    public const int MaxDuration = 100;
    public const int MinIntensity = 5;
    public const int MaxIntensity = 100;

    /// <summary>Set by the platform head before the application starts; null on the desktop.</summary>
    public static IHapticsDevice? Device { get; set; }

    public static bool IsAvailable => Device?.IsAvailable == true;

    public static bool HasAmplitudeControl => Device?.HasAmplitudeControl == true;

    /// <summary>Vibrates once with the saved duration and intensity (if enabled and supported).</summary>
    public static void Buzz()
    {
        IHapticsDevice? device = Device;
        if (device == null || !device.IsAvailable)
        {
            return;
        }

        AppSettings s = SettingsService.Instance.Current;
        if (!s.HapticsEnabled)
        {
            return;
        }

        int duration = Math.Clamp(s.HapticsDuration, MinDuration, MaxDuration);
        int amplitude = (int)Math.Round(Math.Clamp(s.HapticsIntensity, MinIntensity, MaxIntensity) / 100.0 * 255.0);
        try
        {
            device.Vibrate(duration, Math.Clamp(amplitude, 1, 255));
        }
        catch (Exception)
        {
            // A missing or refused vibration must never break the calculator.
        }
    }
}
