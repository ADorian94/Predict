using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Models;
using Predict.Services;

namespace Predict.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const double LbsToKg = 0.45359237;

    private readonly ResultContext _resultContext;
    private readonly AppSettingsService _settings;

    [ObservableProperty]
    private string _weightText = string.Empty;

    [ObservableProperty]
    private string _repsText = "5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RpeColor))]
    private double _rpe = 8.0;

    [ObservableProperty]
    private string _rpeDisplay = "8.0";

    public Color RpeColor => Rpe switch
    {
        <= 7.0 => Color.FromArgb("#34D399"),
        <= 8.0 => Color.FromArgb("#C084FC"),
        <= 9.0 => Color.FromArgb("#60A5FA"),
        _      => Color.FromArgb("#F472B6")
    };

    [ObservableProperty]
    private string _weightError = string.Empty;

    [ObservableProperty]
    private string _repsError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightUnit))]
    [NotifyPropertyChangedFor(nameof(WeightLabel))]
    private bool _isLbs = false;

    public string WeightUnit  => IsLbs ? "lbs" : "kg";
    public string WeightLabel => IsLbs ? "WEIGHT (lbs)" : "WEIGHT (kg)";

    public MainViewModel(ResultContext resultContext, AppSettingsService settings)
    {
        _resultContext = resultContext;
        _settings      = settings;

        WeightText = Preferences.Default.Get(AppConstants.PrefWeight, string.Empty);
        RepsText   = Preferences.Default.Get(AppConstants.PrefReps, "5");
        Rpe        = Preferences.Default.Get(AppConstants.PrefRpe, 8.0);
        IsLbs      = _settings.IsLbs;
    }

    public void RefreshUnit()
    {
        IsLbs = _settings.IsLbs;
    }

    partial void OnIsLbsChanged(bool value)
    {
        // Errors describe the previous unit; wait for Estimate to validate again.
        WeightError = string.Empty;
    }

    partial void OnRpeChanged(double value)
    {
        var snapped = Math.Round(value * 2) / 2.0;
        if (Math.Abs(snapped - value) > 0.001)
        {
            Rpe = snapped;
            return;
        }
        RpeDisplay = $"{snapped:0.0}";
        HapticService.Click();
    }

    partial void OnWeightTextChanged(string? oldValue, string newValue)
    {
        // Reject the whole edit instead of silently truncating a pasted weight.
        if (!System.Text.RegularExpressions.Regex.IsMatch(newValue, @"\A[0-9]*(?:[.,][0-9]{0,2})?\z"))
        {
            WeightText = oldValue ?? string.Empty;
            // Errors must describe the displayed, accepted value, not a rejected edit.
            // Estimate still validates the actual lower/upper bounds.
            WeightError = string.Empty;
            return;
        }
        if (newValue.StartsWith('.') || newValue.StartsWith(','))
            WeightText = "0" + newValue;
        WeightError = string.Empty;
    }

    partial void OnRepsTextChanged(string value)
    {
        RepsError = string.Empty;
        OnPropertyChanged(nameof(CanIncrementReps));
        OnPropertyChanged(nameof(CanDecrementReps));
        IncrementRepsCommand.NotifyCanExecuteChanged();
        DecrementRepsCommand.NotifyCanExecuteChanged();
    }

    public bool CanIncrementReps => string.IsNullOrWhiteSpace(RepsText)
        || (int.TryParse(RepsText, out var reps) && reps >= AppConstants.MinReps && reps < AppConstants.MaxReps);
    public bool CanDecrementReps => int.TryParse(RepsText, out var reps)
        && reps > AppConstants.MinReps && reps <= AppConstants.MaxReps;

    [RelayCommand(CanExecute = nameof(CanIncrementReps))]
    private void IncrementReps()
    {
        if (int.TryParse(RepsText, out var r) && r < AppConstants.MaxReps)
        {
            RepsText = (r + 1).ToString();
            HapticService.Click();
        }
        else if (string.IsNullOrWhiteSpace(RepsText))
        {
            RepsText = "1";
            HapticService.Click();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDecrementReps))]
    private void DecrementReps()
    {
        if (int.TryParse(RepsText, out var r) && r > AppConstants.MinReps)
        {
            RepsText = (r - 1).ToString();
            HapticService.Click();
        }
    }

    [RelayCommand]
    private async Task Calculate()
    {
        var weightOk = TryParseWeight(out var weight);
        var repsOk   = TryParseReps(out var reps);

        if (!weightOk)
        {
            WeightError = string.IsNullOrWhiteSpace(WeightText)
                ? "Enter a weight!"
                : !System.Text.RegularExpressions.Regex.IsMatch(WeightText, @"\A[0-9]+(?:[.,][0-9]{0,2})?\z")
                    ? "Use digits and up to two decimal places (dot or comma)."
                    : weight <= 0
                        ? $"Enter a weight greater than 0 {WeightUnit}!"
                        : $"Enter a weight no greater than {(IsLbs ? AppConstants.MaxWeightLbs : AppConstants.MaxWeightKg)} {WeightUnit}!";
        }
        if (!repsOk) RepsError = "Enter a whole number between 1 and 20!";
        if (!weightOk || !repsOk) return;

        WeightError = string.Empty;
        RepsError   = string.Empty;

        Preferences.Default.Set(AppConstants.PrefWeight, WeightText);
        Preferences.Default.Set(AppConstants.PrefReps,   RepsText);
        Preferences.Default.Set(AppConstants.PrefRpe,    Rpe);

        HapticService.Click();

        var weightKg = IsLbs ? weight * LbsToKg : weight;
        var result   = OneRmCalculator.Calculate(weightKg, reps, Rpe, IsLbs, _settings.IsRounded);
        _resultContext.Current = result;
        await HistoryService.SaveAsync(result);

        MixpanelService.Track("estimate_calculated", new()
        {
            ["unit"]         = IsLbs ? "lbs" : "kg",
            ["is_rounded"]   = _settings.IsRounded,
        });

        await Shell.Current.GoToAsync(nameof(ResultPage));
    }

    private bool TryParseWeight(out double weight)
    {
        if (string.IsNullOrWhiteSpace(WeightText)
            || !System.Text.RegularExpressions.Regex.IsMatch(WeightText, @"\A[0-9]+(?:[.,][0-9]{0,2})?\z"))
        { weight = 0; return false; }
        var normalized = WeightText.Replace(',', '.');
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out weight) || !double.IsFinite(weight) || weight <= 0)
            return false;
        return weight <= (IsLbs ? AppConstants.MaxWeightLbs : AppConstants.MaxWeightKg);
    }

    private bool TryParseReps(out int reps) =>
        int.TryParse(RepsText?.Trim(), out reps)
        && reps >= AppConstants.MinReps
        && reps <= AppConstants.MaxReps;

    [RelayCommand]
    private static async Task ShowHistory()
    {
        MixpanelService.Track("history_opened");
        await Shell.Current.GoToAsync(nameof(HistoryPage));
    }

    [RelayCommand]
    private static async Task ShowSettings()
    {
        MixpanelService.Track("settings_opened");
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}
