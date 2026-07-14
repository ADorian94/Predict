using Android.App;
using Android.Content.PM;
using Android.OS;
using Plugin.MauiMtAdmob;
using Predict.Services;

namespace Predict;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-3940256099942544~3347511713");
        ThemeService.ApplyStatusBarFromSaved();
    }
}
