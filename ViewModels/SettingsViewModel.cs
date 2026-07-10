using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Predict.Services;

namespace Predict.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;

    // ── Unit toggle ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KgBackground))]
    [NotifyPropertyChangedFor(nameof(LbsBackground))]
    [NotifyPropertyChangedFor(nameof(KgBorder))]
    [NotifyPropertyChangedFor(nameof(LbsBorder))]
    [NotifyPropertyChangedFor(nameof(KgTextColor))]
    [NotifyPropertyChangedFor(nameof(LbsTextColor))]
    private bool _isLbs;

    public Brush KgBackground  => new SolidColorBrush(IsLbs ? Color.FromArgb("#13132B") : Color.FromArgb("#1A1035"));
    public Brush LbsBackground => new SolidColorBrush(IsLbs ? Color.FromArgb("#1A1035") : Color.FromArgb("#13132B"));
    public Brush KgBorder      => new SolidColorBrush(IsLbs ? Color.FromArgb("#2D2D5E") : Color.FromArgb("#7C3AED"));
    public Brush LbsBorder     => new SolidColorBrush(IsLbs ? Color.FromArgb("#7C3AED") : Color.FromArgb("#2D2D5E"));
    public Color KgTextColor   => IsLbs ? Color.FromArgb("#505870") : Color.FromArgb("#C084FC");
    public Color LbsTextColor  => IsLbs ? Color.FromArgb("#C084FC") : Color.FromArgb("#505870");

    partial void OnIsLbsChanged(bool value) => _settings.IsLbs = value;

    [RelayCommand] private void SelectKg()  => IsLbs = false;
    [RelayCommand] private void SelectLbs() => IsLbs = true;

    // ── Rounding toggle ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoundedBackground))]
    [NotifyPropertyChangedFor(nameof(RawBackground))]
    [NotifyPropertyChangedFor(nameof(RoundedBorder))]
    [NotifyPropertyChangedFor(nameof(RawBorder))]
    [NotifyPropertyChangedFor(nameof(RoundedTextColor))]
    [NotifyPropertyChangedFor(nameof(RawTextColor))]
    private bool _isRounded;

    public Brush RoundedBackground => new SolidColorBrush(IsRounded ? Color.FromArgb("#1A1035") : Color.FromArgb("#13132B"));
    public Brush RawBackground     => new SolidColorBrush(IsRounded ? Color.FromArgb("#13132B") : Color.FromArgb("#1A1035"));
    public Brush RoundedBorder     => new SolidColorBrush(IsRounded ? Color.FromArgb("#7C3AED") : Color.FromArgb("#2D2D5E"));
    public Brush RawBorder         => new SolidColorBrush(IsRounded ? Color.FromArgb("#2D2D5E") : Color.FromArgb("#7C3AED"));
    public Color RoundedTextColor  => IsRounded ? Color.FromArgb("#C084FC") : Color.FromArgb("#505870");
    public Color RawTextColor      => IsRounded ? Color.FromArgb("#505870") : Color.FromArgb("#C084FC");

    partial void OnIsRoundedChanged(bool value) => _settings.IsRounded = value;

    [RelayCommand] private void SelectRounded() => IsRounded = true;
    [RelayCommand] private void SelectRaw()     => IsRounded = false;

    // ── Init & navigation ────────────────────────────────────────
    public SettingsViewModel(AppSettingsService settings)
    {
        _settings  = settings;
        _isLbs     = settings.IsLbs;
        _isRounded = settings.IsRounded;
    }

    [RelayCommand]
    private static async Task GoBack() => await Shell.Current.GoToAsync("..");
}
