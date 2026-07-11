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

    public Brush KgBackground  => new SolidColorBrush(IsLbs ? ThemeService.CardBg : ThemeService.AccentCardBg);
    public Brush LbsBackground => new SolidColorBrush(IsLbs ? ThemeService.AccentCardBg : ThemeService.CardBg);
    public Brush KgBorder      => new SolidColorBrush(IsLbs ? ThemeService.CardBorderCol : ThemeService.AccentLight);
    public Brush LbsBorder     => new SolidColorBrush(IsLbs ? ThemeService.AccentLight : ThemeService.CardBorderCol);
    public Color KgTextColor   => IsLbs ? ThemeService.MutedText : ThemeService.AccentLight;
    public Color LbsTextColor  => IsLbs ? ThemeService.AccentLight : ThemeService.MutedText;

    partial void OnIsLbsChanged(bool value)
    {
        _settings.IsLbs = value;
        MixpanelService.Track("unit_changed", new() { ["unit"] = value ? "lbs" : "kg" });
    }

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

    public Brush RoundedBackground => new SolidColorBrush(IsRounded ? ThemeService.AccentCardBg : ThemeService.CardBg);
    public Brush RawBackground     => new SolidColorBrush(IsRounded ? ThemeService.CardBg : ThemeService.AccentCardBg);
    public Brush RoundedBorder     => new SolidColorBrush(IsRounded ? ThemeService.AccentLight : ThemeService.CardBorderCol);
    public Brush RawBorder         => new SolidColorBrush(IsRounded ? ThemeService.CardBorderCol : ThemeService.AccentLight);
    public Color RoundedTextColor  => IsRounded ? ThemeService.AccentLight : ThemeService.MutedText;
    public Color RawTextColor      => IsRounded ? ThemeService.MutedText : ThemeService.AccentLight;

    partial void OnIsRoundedChanged(bool value)
    {
        _settings.IsRounded = value;
        MixpanelService.Track("rounding_changed", new() { ["is_rounded"] = value });
    }

    [RelayCommand] private void SelectRounded() => IsRounded = true;
    [RelayCommand] private void SelectRaw()     => IsRounded = false;

    // ── Theme toggle (2-state) ───────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DarkBackground))]
    [NotifyPropertyChangedFor(nameof(LightBackground))]
    [NotifyPropertyChangedFor(nameof(DarkBorder))]
    [NotifyPropertyChangedFor(nameof(LightBorder))]
    [NotifyPropertyChangedFor(nameof(DarkTextColor))]
    [NotifyPropertyChangedFor(nameof(LightTextColor))]
    private string _currentTheme = AppConstants.ThemeDark;

    private Brush InactiveBg     => new SolidColorBrush(ThemeService.CardBg);
    private Brush ActiveBg       => new SolidColorBrush(ThemeService.AccentCardBg);
    private Brush InactiveBorder => new SolidColorBrush(ThemeService.CardBorderCol);
    private Brush ActiveBorder   => new SolidColorBrush(ThemeService.AccentLight);
    private Color InactiveText   => ThemeService.MutedText;
    private Color ActiveText     => ThemeService.AccentLight;

    public Brush DarkBackground  => CurrentTheme == AppConstants.ThemeDark  ? ActiveBg : InactiveBg;
    public Brush LightBackground => CurrentTheme == AppConstants.ThemeLight ? ActiveBg : InactiveBg;
    public Brush DarkBorder      => CurrentTheme == AppConstants.ThemeDark  ? ActiveBorder : InactiveBorder;
    public Brush LightBorder     => CurrentTheme == AppConstants.ThemeLight ? ActiveBorder : InactiveBorder;
    public Color DarkTextColor   => CurrentTheme == AppConstants.ThemeDark  ? ActiveText : InactiveText;
    public Color LightTextColor  => CurrentTheme == AppConstants.ThemeLight ? ActiveText : InactiveText;

    partial void OnCurrentThemeChanged(string value)
    {
        _settings.Theme = value;
        ThemeService.Apply(value);
        NotifyAllToggleColors();
        MixpanelService.Track("theme_changed", new() { ["theme"] = value });
    }

    [RelayCommand] private void SelectDark()  => CurrentTheme = AppConstants.ThemeDark;
    [RelayCommand] private void SelectLight() => CurrentTheme = AppConstants.ThemeLight;

    private void NotifyAllToggleColors()
    {
        OnPropertyChanged(nameof(KgBackground));
        OnPropertyChanged(nameof(LbsBackground));
        OnPropertyChanged(nameof(KgBorder));
        OnPropertyChanged(nameof(LbsBorder));
        OnPropertyChanged(nameof(KgTextColor));
        OnPropertyChanged(nameof(LbsTextColor));
        OnPropertyChanged(nameof(RoundedBackground));
        OnPropertyChanged(nameof(RawBackground));
        OnPropertyChanged(nameof(RoundedBorder));
        OnPropertyChanged(nameof(RawBorder));
        OnPropertyChanged(nameof(RoundedTextColor));
        OnPropertyChanged(nameof(RawTextColor));
    }

    // ── Init & navigation ────────────────────────────────────────
    public SettingsViewModel(AppSettingsService settings)
    {
        _settings     = settings;
        _isLbs        = settings.IsLbs;
        _isRounded    = settings.IsRounded;
        _currentTheme = settings.Theme;
    }

    [RelayCommand]
    private static async Task GoBack() => await Shell.Current.GoToAsync("..");
}
