using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Predict.Services;

namespace Predict;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private AndroidAdsConsentBackend? AdsConsent => IPlatformApplication.Current?.Services.GetService<AndroidAdsConsentBackend>();
    protected override void OnResume()
    {
        base.OnResume();
        AdsConsent?.Resume(this);
    }
    protected override void OnPause()
    {
        AdsConsent?.Pause(this);
        base.OnPause();
    }
    protected override void OnDestroy()
    {
        AdsConsent?.Detach(this);
        base.OnDestroy();
    }
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AdsConsent?.Attach(this);
        ThemeService.ApplyStatusBarFromSaved();
    }
}
