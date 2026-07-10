using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Models;
using Predict.Services;

namespace Predict.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    [ObservableProperty]
    private OneRmResult? _result;

    [ObservableProperty]
    private string _animatedBestEstimate = "0.0";

    private readonly CancellationTokenSource _cts = new();

    public ResultViewModel(ResultContext ctx)
    {
        Result = ctx.Current;
        _ = AnimateCounterAsync(_cts.Token);
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

    public string RoundingNote => Result?.IsRounded == true
        ? "(avg of 8 formulas, rounded to 2.5)"
        : "(avg of 8 formulas)";

    public string WeightDisplay => Result is null ? "" :
        Result.IsLbs
            ? $"{Result.Weight / 0.45359237:0.#} lbs"
            : $"{Result.Weight:0.#} kg";

    public IReadOnlyList<FormulaEntry> Formulas
    {
        get
        {
            if (Result is null) return [];
            var u = Result.Unit;
            return [
                new("RTS RPE",  $"{Result.RpeTableDisplay:0.0} {u}",  "1RM = weight / intensity%"),
                new("Epley",    $"{Result.EpleyDisplay:0.0} {u}",     "w*(1 + r/30)"),
                new("Brzycki",  $"{Result.BrzycziDisplay:0.0} {u}",   "w*36/(37-r)"),
                new("Lander",   $"{Result.LanderDisplay:0.0} {u}",    "100w/(101.3-2.671r)"),
                new("Lombardi", $"{Result.LombardiDisplay:0.0} {u}",  "w * r^0.10"),
                new("Mayhew",   $"{Result.MayhewDisplay:0.0} {u}",    "100w/(52.2+41.9e^-0.055r)"),
                new("O'Conner", $"{Result.OConnerDisplay:0.0} {u}",   "w*(1 + 0.025r)"),
                new("Wathan",   $"{Result.WathanDisplay:0.0} {u}",    "100w/(48.8+53.8e^-0.075r)"),
            ];
        }
    }

    [RelayCommand]
    private async Task Share()
    {
        if (Result is null) return;
        var lines = string.Join("\n", Formulas.Select(f => $"  {f.Name}: {f.ResultDisplay}"));
        var text =
            $"Predict — 1RM Estimate\n" +
            $"Weight: {WeightDisplay} | Reps: {Result.Reps} | RPE: {Result.Rpe:0.0}\n\n" +
            $"Best estimate: {AnimatedBestEstimate}\n\n" +
            $"Formulas:\n{lines}";

        MixpanelService.Track("result_shared");
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
            new Microsoft.Maui.ApplicationModel.DataTransfer.ShareTextRequest
            {
                Title = "1RM Result",
                Text  = text
            });
    }

    [RelayCommand]
    private static async Task GoBack() =>
        await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private static async Task NewEstimation() =>
        await Shell.Current.GoToAsync("//MainPage");
}
