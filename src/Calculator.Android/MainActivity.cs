using Android.App;
using Android.Content.PM;
using Android.OS;
using CalcApp.Services;
using Avalonia;
using Avalonia.Android;

// The currency converter downloads the exchange rates.
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
// Vibration feedback when a key is touched.
[assembly: UsesPermission(Android.Manifest.Permission.Vibrate)]

namespace CalculatorAndroid;

[Activity(
    Label = "wCalculator",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<CalcApp.App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Before the application starts, so that the settings page knows whether the device can vibrate.
        Haptics.Device = new AndroidHaptics(this);
        base.OnCreate(savedInstanceState);
    }
}
