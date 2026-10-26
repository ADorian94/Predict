namespace Predict.Services;

// Narrow boundary: implementations marshal callbacks onto the UI thread.
public interface IAdsConsentBackend
{
    bool CanRequestAds { get; }
    bool PrivacyOptionsRequired { get; }
    void RequestUpdate(Action<bool> completed);
    void GatherConsent(Action completed);
    void ShowPrivacyOptions(Action completed);
    void InitializeAds(Action completed);
    void SetAdsEnabled(bool enabled);
    void ClearInterstitial();
    void LoadInterstitial();
    bool IsInterstitialLoaded();
    void ShowInterstitial(Action completed);
}

// All entry points and backend callbacks are serialized on the main/UI thread.
// No consent decision is persisted here: every request checks the real UMP state.
public sealed class AdsConsentCoordinator
{
    private readonly IAdsConsentBackend? backend;
    private bool started, foreground, initializing, initialized, updateCompleted;
    private bool gatherPending, gathering, privacyBusy;
    private bool publishedReady, publishedPrivacy;
    private int generation;
    private bool interstitialWanted, preloaded, interstitialShowing;
    private TaskCompletionSource? navigation;
    private IDisposable? interstitialPresentation;
    public void PreloadInterstitial()
    {
        interstitialWanted = true;
        if (backend is null || !IsReady || preloaded || interstitialShowing) return;
        preloaded = true;
        try { backend.LoadInterstitial(); } catch { InterstitialLoadFailed(); }
    }
    // Release admission only; a later explicit result request may retry, never a
    // synchronous retry loop on network/no-fill failures.
    internal void InterstitialLoadFailed() => preloaded = false;
    public Task TryShowInterstitialAsync()
    {
        if (backend is null || !IsReady || interstitialShowing || AdsConsentPresentation.IsBusy) return Task.CompletedTask;
        try
        {
            if (!backend.IsInterstitialLoaded() || !IsReady) return Task.CompletedTask;
            var presentation = AdsConsentPresentation.TryEnter();
            if (presentation is null) return Task.CompletedTask;
            interstitialPresentation = presentation;
            interstitialShowing = true;
            navigation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task = navigation.Task;
            bool completed = false;
            int current = generation;
            backend.ShowInterstitial(() =>
            {
                presentation.Dispose();
                if (completed || current != generation) return;
                interstitialPresentation = null;
                completed = true;
                interstitialShowing = false;
                navigation?.TrySetResult(); navigation = null;
                preloaded = false;
                Refresh();
                if (interstitialWanted) PreloadInterstitial();
            });
            return task;
        }
        catch
        {
            interstitialPresentation?.Dispose(); interstitialPresentation = null;
            interstitialShowing = false;
            navigation?.TrySetResult(); navigation = null;
            return Task.CompletedTask;
        }
    }
    public AdsConsentCoordinator(IAdsConsentBackend? backend = null) => this.backend = backend;
    public event EventHandler? Changed;
    public bool IsReady => started && foreground && !gatherPending && !gathering && !privacyBusy && initialized && ReadPermission();
    public bool PrivacyOptionsRequired
    {
        get { try { return backend?.PrivacyOptionsRequired == true; } catch { return false; } }
    }
    private bool ReadPermission()
    {
        try { return backend?.CanRequestAds == true; } catch { return false; }
    }

    public void Start()
    {
        if (backend is null || started) return;
        started = foreground = true;
        int current = ++generation;
        try { backend.RequestUpdate(success =>
        {
            if (current != generation || updateCompleted) return;
            updateCompleted = true;
            gatherPending = success;
            Refresh();
        }); } catch { updateCompleted = true; }
        Refresh();
    }
    public void Pause() { foreground = false; Publish(); }
    public void Resume() { if (!started) return; foreground = true; Refresh(); }
    public void Detach()
    {
        ++generation;
        interstitialPresentation?.Dispose(); interstitialPresentation = null;
        interstitialShowing = false;
        navigation?.TrySetResult(); navigation = null;
        started = foreground = updateCompleted = gatherPending = gathering = privacyBusy = false;
        Publish();
    }
    public void ShowPrivacyOptions()
    {
        if (backend is null || !started || !foreground || gathering || privacyBusy || interstitialShowing || !PrivacyOptionsRequired) return;
        privacyBusy = true;
        Publish();
        int current = generation;
        try { backend.ShowPrivacyOptions(() =>
        {
            if (current != generation) return;
            privacyBusy = false;
            Refresh();
        }); } catch { privacyBusy = false; Refresh(); }
    }
    private void Publish()
    {
        bool ready = IsReady, privacy = PrivacyOptionsRequired;
        backend?.SetAdsEnabled(ready);
        if (ready == publishedReady && privacy == publishedPrivacy) return;
        if (!ready)
        {
            if (publishedReady && initialized)
            {
                try { backend?.ClearInterstitial(); } catch { }
            }
            preloaded = false;
            navigation?.TrySetResult();
        }
        publishedReady = ready; publishedPrivacy = privacy;
        Changed?.Invoke(this, EventArgs.Empty);
        if (ready && interstitialWanted) PreloadInterstitial();
    }
    private void Refresh()
    {
        if (backend is null) return;
        if (started && foreground && gatherPending && !gathering && !privacyBusy && !interstitialShowing)
        {
            gatherPending = false;
            gathering = true;
            Publish();
            int current = generation;
            try { backend.GatherConsent(() =>
            {
                if (current != generation || !gathering) return;
                gathering = false; Refresh();
            }); } catch { gathering = false; }
        }
        Publish();
        if (!started || !foreground || gathering || privacyBusy || !ReadPermission() || initializing) return;
        initializing = true;
        try { backend.InitializeAds(() => { initialized = true; Refresh(); }); }
        catch { Publish(); }
    }
}
