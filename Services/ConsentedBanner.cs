using Microsoft.Extensions.DependencyInjection;
using Plugin.MauiMtAdmob.Controls;
using Plugin.MauiMtAdmob.Extra;
using System.Diagnostics.CodeAnalysis;

namespace Predict.Services;

// An empty slot, NOT an invisible MTAdView: ConnectHandler auto-loads in the free plugin.
public sealed class ConsentedBanner : Grid
{
    private AdsConsentCoordinator? consent;
    private MTAdView? banner;
    private double reservedHeight;
    private Thickness reservedMargin;
    private bool measured;
    public ConsentedBanner()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    private void OnLoaded(object? sender, EventArgs args)
    {
        if (!measured)
        {
            reservedHeight = HeightRequest;
            reservedMargin = Margin;
            measured = true;
        }
        consent = Handler?.MauiContext?.Services.GetService<AdsConsentCoordinator>();
        if (consent is not null) { consent.Changed -= OnConsentChanged; consent.Changed += OnConsentChanged; }
        UpdateBanner();
    }
    private void OnUnloaded(object? sender, EventArgs args)
    {
        if (consent is not null) consent.Changed -= OnConsentChanged;
        consent = null;
        RemoveBanner();
    }
    private void OnConsentChanged(object? sender, EventArgs args) => UpdateBanner();
    private void UpdateBanner()
    {
        if (consent?.IsReady != true) { RemoveBanner(); return; }
        if (banner is not null) return;
        HeightRequest = reservedHeight;
        Margin = reservedMargin;
        try
        {
            banner = new MTAdView
            {
                AdsId = AdUnits.Banner,
                AdSize = BannerSize.Banner,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            BannerAdLifecycle.SetObserve(banner, true);
            Children.Add(banner);
        }
        catch (Exception)
        {
            System.Diagnostics.Trace.WriteLine("Optional banner unavailable", "PredictAds");
            RemoveBanner();
        }
    }
#if ANDROID
    [DynamicDependency("Destroy", "Plugin.MauiMtAdmob.Platforms.Android.MTAdViewHandler", "Plugin.MauiMtAdmob")]
#endif
    private void RemoveBanner()
    {
        if (banner is not null)
        {
            var old = banner; banner = null;
            BannerAdLifecycle.SetObserve(old, false);
#if ANDROID
            if (old.Handler?.PlatformView is Android.Gms.Ads.AdView native)
            {
                Teardown(() => native.Pause());
                Teardown(() => native.AdListener = null!);
                Teardown(() => native.Destroy()); // DisconnectHandler alone does NOT destroy native refresh resources.
            }
#endif
            // Plugin 2.4.0's handler and its interface are INTERNAL. Its public
            // Destroy method unsubscribes singleton events; Disconnect alone does not.
            // Preserve that exact installed method for trimmed Android builds.
            Teardown(() => old.Handler?.GetType().GetMethod("Destroy", Type.EmptyTypes)?.Invoke(old.Handler, null));
            Teardown(() => old.Handler?.DisconnectHandler());
            Children.Remove(old);
        }
        HeightRequest = 0;
        Margin = new Thickness(0);
    }
    private static void Teardown(Action action)
    {
        try { action(); }
        catch (Exception) { System.Diagnostics.Trace.WriteLine("Optional banner teardown unavailable", "PredictAds"); }
    }
}
