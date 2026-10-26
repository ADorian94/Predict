using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Models;
using Predict.Services;

namespace Predict.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly AdsConsentCoordinator _ads;
    private static int _exitCount = 0;

    [ObservableProperty]
    private OneRmResult? _result;

    [ObservableProperty]
    private string _animatedBestEstimate = "0.0";

    private readonly CancellationTokenSource _cts = new();

    public ResultViewModel(ResultContext ctx, AdsConsentCoordinator ads)
    {
        _ads = ads;
        Result = ctx.Current;
        _ = AnimateCounterAsync(_cts.Token);
        _ads.PreloadInterstitial();
    }

    public void CancelAnimation() => _cts.Cancel();

    private async Task AnimateCounterAsync(CancellationToken ct)
    {
        if (Result is null) return;
        var target = Result.BestEstimate;
        const int steps   = 30;
        const int delayMs = 20;

        try
        {
            for (int i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();
                var eased = 1 - Math.Pow(1.0 - (double)i / steps, 3);
                AnimatedBestEstimate = $"{target * eased:0.0} {Result.Unit}";
                if (i % 5 == 0) HapticService.Click();
                await Task.Delay(delayMs, ct);
            }

            AnimatedBestEstimate = $"{target:0.0} {Result.Unit}";
            HapticService.Click();
        }
        catch (OperationCanceledException) { }
    }

    public string EstimationDate => DateTime.Now.ToString("yyyy.MM.dd HH:mm");

    public string RoundingNote => Result?.RoundingNote ?? string.Empty;

    public string WeightDisplay => Result is null ? "" :
        Result.IsLbs
            ? $"{Result.Weight / 0.45359237:0.#} lbs"
            : $"{Result.Weight:0.#} kg";

    public IReadOnlyList<FormulaEntry> Formulas => Result?.Formulas ?? [];

    [RelayCommand]
    private async Task Share()
    {
        if (Result is null) return;
        var lines = string.Join("\n", Formulas.Select(f => $"  {f.Name}: {f.ResultDisplay}"));
        var text =
            $"Predict â€” 1RM Estimate\n" +
            $"Weight: {WeightDisplay} | Reps: {Result.Reps} | RPE: {Result.Rpe:0.0}\n\n" +
            $"Best estimate: {AnimatedBestEstimate}\n{RoundingNote}\n{Result.RpeAvailabilityNote}\n\n" +
            $"Formulas:\n{lines}";

        MixpanelService.Track("result_shared");
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
            new Microsoft.Maui.ApplicationModel.DataTransfer.ShareTextRequest
            {
                Title = "1RM Result",
                Text  = text
            });
    }

    private async Task TryShowInterstitialAsync()
    {
        _exitCount++;
        if (_exitCount % 3 == 0) await _ads.TryShowInterstitialAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await TryShowInterstitialAsync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task NewEstimation()
    {
        await TryShowInterstitialAsync();
        await Shell.Current.GoToAsync("//MainPage");
    }
}
