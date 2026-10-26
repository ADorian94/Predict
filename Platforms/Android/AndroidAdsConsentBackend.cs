using System.Diagnostics.CodeAnalysis;
using Android.App;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Plugin.MauiMtAdmob;
using Plugin.MauiMtAdmob.Extra;
using Xamarin.Google.UserMesssagingPlatform;

namespace Predict.Services;

// Only this Android boundary touches UMP/the advertising SDK. The coordinator is
// framework-free and all callback transitions are posted onto the Android UI queue.
public sealed class AndroidAdsConsentBackend : IAdsConsentBackend
{
    private const string AppId = "ca-app-pub-3012910791046560~7179364438";
    private static readonly Handler Ui = new(Looper.MainLooper!);
    private WeakReference<MainActivity>? activity;
    private IConsentInformation? information;
    private CancellationTokenSource lifetime = new();
    private TaskCompletionSource resumed = new();
    private bool foreground, pluginInitialized, loading, invalidatedLoad, reloadAfterLoad;
    private readonly List<Java.Lang.Object> listeners = [];
    private readonly AdsConsentCoordinator coordinator;
    private Action? finishInterstitial;
    public AndroidAdsConsentBackend()
    {
        coordinator = new AdsConsentCoordinator(this);
        CrossMauiMTAdmob.Current.IsEnabled = false;
    }
    public AdsConsentCoordinator Coordinator => coordinator;
    public bool CanRequestAds => information?.CanRequestAds() == true;
    public bool PrivacyOptionsRequired => information?.PrivacyOptionsRequirementStatus ==
        ConsentInformationPrivacyOptionsRequirementStatus.Required;

    public void Attach(MainActivity current)
    {
        if (activity?.TryGetTarget(out var old) == true && ReferenceEquals(old, current)) return;
        DetachCurrent();
        activity = new(current);
    }
    public void Resume(MainActivity current)
    {
        if (!IsCurrent(current)) return;
        foreground = true;
        if (pluginInitialized) SetPluginActivity(current);
        resumed.TrySetResult();
        if (!started) { started = true; coordinator.Start(); }
        else coordinator.Resume();
    }
    private bool started;
    public void Pause(MainActivity current)
    {
        if (!IsCurrent(current)) return;
        foreground = false;
        resumed = new TaskCompletionSource();
        coordinator.Pause();
    }
    public void Detach(MainActivity current) { if (IsCurrent(current)) DetachCurrent(); }
    private void DetachCurrent()
    {
        coordinator.Detach();
        // The pinned plugin reports terminal events globally, without ad identity.
        // Keep the old native subscription as a retirement barrier: Detach already
        // releases navigation/the presentation lease, but cannot prove native close.
        // Until its terminal event arrives, skip subsequent interstitial shows.
        // No Activity is captured by these handlers; missing events only omit ads.
        if (pluginInitialized) { ClearInterstitial(); SetPluginActivity(null); }
        started = foreground = false;
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new CancellationTokenSource();
        activity = null;
        resumed.TrySetResult();
        // Native callbacks may still arrive; generation/token guards ignore them.
        listeners.Clear();
    }
    private bool IsCurrent(MainActivity current) => activity?.TryGetTarget(out var a) == true && ReferenceEquals(a, current);
    private MainActivity? ActiveActivity => foreground && activity?.TryGetTarget(out var a) == true && !a.IsFinishing && !a.IsDestroyed ? a : null;
    private static void Post(Action action) => Ui.Post(action);

    public void RequestUpdate(Action<bool> completed)
    {
        var current = ActiveActivity ?? throw new InvalidOperationException("No resumed advertising activity");
        information = UserMessagingPlatform.GetConsentInformation(Android.App.Application.Context);
        var token = lifetime.Token;
        var success = new UpdateSuccess(() => Post(() => { if (!token.IsCancellationRequested) completed(true); }));
        var failure = new UpdateFailure(() => Post(() => { if (!token.IsCancellationRequested) completed(false); }));
        listeners.Add(success); listeners.Add(failure);
        information.RequestConsentInfoUpdate(current, new ConsentRequestParameters.Builder().Build(), success, failure);
    }
    public void GatherConsent(Action completed) => _ = PresentAsync(false, completed, lifetime.Token);
    public void ShowPrivacyOptions(Action completed) => _ = PresentAsync(true, completed, lifetime.Token);
    private async Task PresentAsync(bool privacy, Action completed, CancellationToken token)
    {
        try
        {
            await AdsConsentPresentation.RunAsync(async () =>
            {
                while (!foreground && !token.IsCancellationRequested) await resumed.Task.WaitAsync(token);
                token.ThrowIfCancellationRequested();
                var dismissed = new TaskCompletionSource<bool>();
                PresentNative(privacy, dismissed, token);
                await dismissed.Task.WaitAsync(token);
                return true;
            });
        }
        catch (System.OperationCanceledException) { return; }
        catch (Exception) { System.Diagnostics.Trace.WriteLine("UMP form unavailable", "PredictAds"); }
        if (!token.IsCancellationRequested) Post(completed); // errors re-query UMP, never invent a decision
    }
    private void PresentNative(bool privacy, TaskCompletionSource<bool> dismissed, CancellationToken token)
    {
        var current = ActiveActivity ?? throw new InvalidOperationException("No resumed advertising activity");
        var listener = new FormDismissed(() => Post(() => { if (!token.IsCancellationRequested) dismissed.TrySetResult(true); }));
        listeners.Add(listener);
        if (privacy) UserMessagingPlatform.ShowPrivacyOptionsForm(current, listener);
        else UserMessagingPlatform.LoadAndShowConsentFormIfRequired(current, listener);
    }
    public void InitializeAds(Action completed)
    {
        var current = ActiveActivity ?? throw new InvalidOperationException("No resumed advertising activity");
        EventHandler? initialized = null;
        initialized = (_, _) =>
        {
            CrossMauiMTAdmob.Current.OnMobileAdsInitialized -= initialized;
            // The plugin sets IsPluginInitialised only at the END of Init.
            Post(() => { pluginInitialized = true; SetPluginActivity(ActiveActivity); completed(); });
        };
        CrossMauiMTAdmob.Current.OnMobileAdsInitialized += initialized;
        CrossMauiMTAdmob.Current.OnInterstitialLoaded += (_, _) => Post(OnInterstitialLoaded);
        CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (_, _) => Post(() =>
        {
            bool replace = invalidatedLoad && reloadAfterLoad && coordinator.IsReady;
            loading = invalidatedLoad = reloadAfterLoad = false;
            if (replace) LoadInterstitial(); // A separately admitted current-epoch request, not a failure retry loop.
            else coordinator.InterstitialLoadFailed();
        });
        try
        {
            // Free direct UMP integration; never call the plugin's licensed consent wrapper.
            CrossMauiMTAdmob.Current.Init(current, AppId, initialiseConsentAtStartup: false);
        }
        catch
        {
            CrossMauiMTAdmob.Current.OnMobileAdsInitialized -= initialized;
            throw;
        }
    }
    [DynamicDependency("set_CurrentActivity", "Plugin.MauiMtAdmob.MauiMTAdmob", "Plugin.MauiMtAdmob")]
    private static void SetPluginActivity(MainActivity? current)
    {
        // Pinned plugin 2.4.0 retains Init's Activity in a public getter/private
        // setter. Its interstitial Show uses MAUI Platform.CurrentActivity, but the
        // old getter still leaks a destroyed Activity unless explicitly replaced.
        // Preserve the verified setter; never re-Init (which would reinitialize SDKs).
        CrossMauiMTAdmob.Current.GetType().GetProperty("CurrentActivity")?
            .SetValue(CrossMauiMTAdmob.Current, current);
    }
    public void SetAdsEnabled(bool enabled) => CrossMauiMTAdmob.Current.IsEnabled = enabled;
    public void ClearInterstitial()
    {
        if (!pluginInitialized) return;
        if (loading) invalidatedLoad = true;
        reloadAfterLoad = false;
        CrossMauiMTAdmob.Current.EmptyInterstitialAdsList();
    }
    public void LoadInterstitial()
    {
        if (!pluginInitialized || !coordinator.IsReady) return;
        if (loading) { reloadAfterLoad = true; return; }
        loading = true; invalidatedLoad = false;
        try { CrossMauiMTAdmob.Current.LoadInterstitial(AdUnits.Interstitial); }
        catch
        {
            loading = invalidatedLoad = reloadAfterLoad = false;
            coordinator.InterstitialLoadFailed();
        }
    }
    private void OnInterstitialLoaded()
    {
        loading = false;
        if (invalidatedLoad || !coordinator.IsReady)
        {
            CrossMauiMTAdmob.Current.EmptyInterstitialAdsList();
            invalidatedLoad = false;
            bool retry = reloadAfterLoad; reloadAfterLoad = false;
            if (retry && coordinator.IsReady) LoadInterstitial();
        }
    }
    public bool IsInterstitialLoaded() => finishInterstitial is null && pluginInitialized && !invalidatedLoad && coordinator.IsReady && CrossMauiMTAdmob.Current.IsInterstitialLoaded();
    public void ShowInterstitial(Action completed)
    {
        if (!IsInterstitialLoaded() || ActiveActivity is null) { completed(); return; }
        EventHandler? closed = null;
        EventHandler<MTEventArgs>? failed = null;
        bool finished = false;
        void Complete()
        {
            if (finished) return;
            finished = true;
            finishInterstitial = null;
            CrossMauiMTAdmob.Current.OnInterstitialClosed -= closed;
            CrossMauiMTAdmob.Current.OnInterstitialFailedToShow -= failed;
            Post(completed);
        }
        finishInterstitial = Complete;
        closed = (_, _) => Complete(); failed = (_, _) => Complete();
        CrossMauiMTAdmob.Current.OnInterstitialClosed += closed;
        CrossMauiMTAdmob.Current.OnInterstitialFailedToShow += failed;
        try { CrossMauiMTAdmob.Current.ShowInterstitial(); }
        catch { Complete(); }
    }
    private sealed class UpdateSuccess(Action done) : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateSuccessListener
    { public void OnConsentInfoUpdateSuccess() => done(); }
    private sealed class UpdateFailure(Action done) : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateFailureListener
    { public void OnConsentInfoUpdateFailure(FormError error) => done(); }
    private sealed class FormDismissed(Action done) : Java.Lang.Object, IConsentFormOnConsentFormDismissedListener
    { public void OnConsentFormDismissed(FormError? error) => done(); }
}
