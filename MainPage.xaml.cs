namespace Predict;

public partial class MainPage : ContentPage
{
    private bool _hasAppeared;
    private bool _isAppearing;
    private bool _entranceCompleted;
    private bool _consentQueued;

    public MainPage(ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        Loaded += (_, _) => QueueAnalyticsConsent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isAppearing = true;
        (BindingContext as ViewModels.MainViewModel)?.RefreshUnit();

        if (_hasAppeared)
        {
            QueueAnalyticsConsent();
            return;
        }
        _hasAppeared = true;

        WeightCard.Opacity = 0; WeightCard.TranslationY = 40;
        RepsCard.Opacity   = 0; RepsCard.TranslationY   = 40;
        RpeCard.Opacity    = 0; RpeCard.TranslationY    = 40;

        try
        {
            // These delays preserve the entrance stagger, not popup readiness.
            await Task.WhenAll(
                AnimateCardAsync(WeightCard, 0),
                AnimateCardAsync(RepsCard, 120),
                AnimateCardAsync(RpeCard, 240));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Main page entrance animation failed: {ex}");
        }
        finally
        {
            // Optional presentation must never strand the calculator's inputs.
            WeightCard.Opacity = RepsCard.Opacity = RpeCard.Opacity = 1;
            WeightCard.TranslationY = RepsCard.TranslationY = RpeCard.TranslationY = 0;
            _entranceCompleted = true;
        }
        QueueAnalyticsConsent();
    }

    private static async Task AnimateCardAsync(VisualElement card, int delay)
    {
        if (delay > 0) await Task.Delay(delay);
        await Task.WhenAll(
            card.FadeTo(1, 350),
            card.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        _isAppearing = false;
        base.OnDisappearing();
    }

    private void QueueAnalyticsConsent()
    {
        if (!_isAppearing || !IsLoaded || !_entranceCompleted || _consentQueued) return;

        // MAUI Loaded means attached to the native visual tree; defer presentation
        // until that lifecycle notification has returned to the UI dispatcher.
        _consentQueued = true;
        try
        {
            if (!Dispatcher.Dispatch(async () =>
            {
                try
                {
                    if (!_isAppearing || !IsLoaded) return;
                    await Services.MixpanelService.EnsureAnalyticsConsentAsync(() =>
                        Services.AdsConsentPresentation.RunAsync(() =>
                        {
                            // Recheck after waiting for a separate advertising form.
                            if (!_isAppearing || !IsLoaded) throw new OperationCanceledException();
                            return DisplayAlert("Optional usage analytics",
                                Services.MixpanelService.AnalyticsDisclosure, "Allow", "No thanks");
                        }));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Analytics consent presentation failed: {ex}");
                }
                finally { _consentQueued = false; }
            })) _consentQueued = false;
        }
        catch (Exception ex)
        {
            _consentQueued = false;
            System.Diagnostics.Debug.WriteLine($"Analytics consent dispatch failed: {ex}");
        }
    }

    private void CalcButton_Clicked(object sender, EventArgs e)
    {
        CalcButton.ScaleTo(0.96, 80).ContinueWith(_ => CalcButton.ScaleTo(1.0, 80));
    }
}
