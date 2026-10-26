using System;
using Android.Content;
using Android.OS;
using CalcApp.Services;

namespace CalculatorAndroid;

/// <summary>Vibration of the Android device, used for the feedback when a key is touched.</summary>
internal sealed class AndroidHaptics : IHapticsDevice
{
    private readonly Vibrator? _vibrator;

    public AndroidHaptics(Context context)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var manager = context.GetSystemService(Context.VibratorManagerService) as VibratorManager;
                _vibrator = manager?.DefaultVibrator;
            }
            else
            {
#pragma warning disable CA1422, CS0618 // Validate platform compatibility / obsolete: the only way before Android 12.
                _vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CA1422, CS0618
            }
        }
        catch (Exception)
        {
            _vibrator = null;
        }
    }

    public bool IsAvailable => _vibrator != null && _vibrator.HasVibrator;

    public bool HasAmplitudeControl => OperatingSystem.IsAndroidVersionAtLeast(26) && _vibrator != null && _vibrator.HasAmplitudeControl;

    public void Vibrate(int durationMs, int amplitude)
    {
        if (_vibrator == null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            // Without amplitude control the default strength is used (only the duration can change).
            int strength = HasAmplitudeControl ? Math.Clamp(amplitude, 1, 255) : VibrationEffect.DefaultAmplitude;
            using VibrationEffect? effect = VibrationEffect.CreateOneShot(durationMs, strength);
            if (effect != null)
            {
                _vibrator.Vibrate(effect);
            }
        }
        else
        {
#pragma warning disable CA1422, CS0618
            _vibrator.Vibrate(durationMs);
#pragma warning restore CA1422, CS0618
        }
    }
}
