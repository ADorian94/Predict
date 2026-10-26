using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Plugin.MauiMtAdmob.Controls;
using Plugin.MauiMtAdmob.Extra;

namespace Predict.Services;

public static class BannerAdLifecycle
{
    private static readonly ConditionalWeakTable<MTAdView, Subscription> Subscriptions = new();
    private static readonly ConditionalWeakTable<Grid, SlotSnapshot> Slots = new();

    public static readonly BindableProperty ObserveProperty = BindableProperty.CreateAttached(
        "Observe", typeof(bool), typeof(BannerAdLifecycle), false,
        propertyChanged: OnObserveChanged);

    public static bool GetObserve(BindableObject view) => (bool)view.GetValue(ObserveProperty);
    public static void SetObserve(BindableObject view, bool value) => view.SetValue(ObserveProperty, value);

    private static void OnObserveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not MTAdView banner) return;
        if ((bool)newValue)
        {
            Subscriptions.GetValue(banner, view => new Subscription(view));
        }
        else if (Subscriptions.TryGetValue(banner, out var subscription))
        {
            subscription.Detach(banner);
            Subscriptions.Remove(banner);
        }
    }

    private static void Update(MTAdView banner, bool loaded, MTEventArgs? error = null)
    {
        // Plugin 2.4.0 forwards its native listener as sender, not the MAUI view.
        // Subscriptions capture the actual view instead of casting event sender.
        var message = loaded ? "Banner loaded" : $"Banner failed: code={error?.ErrorCode}; {error?.ErrorMessage}";
        Trace.WriteLine(message, "PredictAds");
#if ANDROID
        Android.Util.Log.Info("PredictAds", message);
#endif
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!GetObserve(banner) || banner.Parent is not Grid slot) return;
            var snapshot = Slots.GetValue(slot, grid => new SlotSnapshot(
                grid.Margin, new BannerSlotState(grid.HeightRequest)));
            if (loaded) snapshot.State.OnLoaded();
            else snapshot.State.OnFailed();
            slot.HeightRequest = snapshot.State.Height;
            slot.Margin = snapshot.State.IsCollapsed ? new Thickness(0) : snapshot.Margin;
        });
    }

    private sealed record SlotSnapshot(Thickness Margin, BannerSlotState State);

    private sealed class Subscription
    {
        private readonly EventHandler loaded;
        private readonly EventHandler<MTEventArgs> failed;

        public Subscription(MTAdView banner)
        {
            loaded = (_, _) => Update(banner, true);
            failed = (_, args) => Update(banner, false, args);
            banner.AdsLoaded += loaded;
            banner.AdsFailedToLoad += failed;
        }

        public void Detach(MTAdView banner)
        {
            banner.AdsLoaded -= loaded;
            banner.AdsFailedToLoad -= failed;
        }
    }
}
