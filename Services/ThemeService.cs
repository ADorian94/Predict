namespace Predict.Services;

public static class ThemeService
{
    private static string _current = AppConstants.ThemeDark;
    public static string Current => _current;

    // Cached per-theme colors — updated in Apply(), read cheaply by ViewModel
    private static Color _cardBg        = Color.FromArgb("#13132B");
    private static Color _cardAltBg     = Color.FromArgb("#1E1E3A");
    private static Color _accentCardBg  = Color.FromArgb("#1A1035");
    private static Color _cardBorderCol = Color.FromArgb("#2D2D5E");
    private static Color _accentLight   = Color.FromArgb("#C084FC");
    private static Color _mutedText     = Color.FromArgb("#505870");

    public static Color CardBg        => _cardBg;
    public static Color CardAltBg     => _cardAltBg;
    public static Color AccentCardBg  => _accentCardBg;
    public static Color CardBorderCol => _cardBorderCol;
    public static Color AccentLight   => _accentLight;
    public static Color MutedText     => _mutedText;

    // Same gradient for all themes — created once
    private static readonly LinearGradientBrush EstimateGradient = MakeGradient("#7C3AED", "#2563EB");

    public static void Apply(string theme)
    {
        _current = theme;
        bool light = theme == AppConstants.ThemeLight;

        _cardBg        = Color.FromArgb(light ? "#FFFFFF" : "#13132B");
        _cardAltBg     = Color.FromArgb(light ? "#F0EDFF" : "#1E1E3A");
        _accentCardBg  = Color.FromArgb(light ? "#EDE8FF" : "#1A1035");
        _cardBorderCol = Color.FromArgb(light ? "#DDD8FF" : "#2D2D5E");
        _accentLight   = Color.FromArgb(light ? "#7C3AED" : "#C084FC");
        _mutedText     = Color.FromArgb(light ? "#8C8AB0" : "#505870");

        var r = Application.Current!.Resources;
        if (light) ApplyLight(r); else ApplyDark(r);
#if ANDROID
        ApplyStatusBar(light);
#endif
    }

#if ANDROID
    private static void ApplyStatusBar(bool light)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window == null) return;

        activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor(light ? "#F4F5FF" : "#08081C"));

        var flags = (Android.Views.SystemUiFlags)activity.Window.DecorView.SystemUiFlags;
        if (light)
            flags |= Android.Views.SystemUiFlags.LightStatusBar;
        else
            flags &= ~Android.Views.SystemUiFlags.LightStatusBar;
        activity.Window.DecorView.SystemUiFlags = flags;
    }
#endif

    private static void ApplyDark(ResourceDictionary r)
    {
        r["PageBackground"]  = Color.FromArgb("#08081C");
        r["TextPrimary"]     = Color.FromArgb("#FFFFFF");
        r["TextSecondary"]   = Color.FromArgb("#A0AACC");
        r["TextMuted"]       = Color.FromArgb("#505870");
        r["LabelMuted"]      = Color.FromArgb("#8C9EC6");
        r["LabelSubtitle"]   = Color.FromArgb("#B4BEDC");
        r["AccentLight"]     = Color.FromArgb("#C084FC");
        r["AccentPrimary"]   = Color.FromArgb("#7C3AED");
        r["AccentBlue"]      = Color.FromArgb("#60A5FA");
        r["AccentPink"]      = Color.FromArgb("#F472B6");
        r["ErrorColor"]      = Color.FromArgb("#F472B6");
        r["PlaceholderText"] = Color.FromArgb("#6B7BA0");
        r["DividerColor"]    = Color.FromArgb("#1A1A3A");
        r["InputBackground"] = Color.FromArgb("#06061A");
        r["SliderTrack"]     = Color.FromArgb("#2D2D5E");

        r["CardBackgroundBrush"]       = new SolidColorBrush(Color.FromArgb("#13132B"));
        r["CardAltBackgroundBrush"]    = new SolidColorBrush(Color.FromArgb("#1E1E3A"));
        r["AccentCardBackgroundBrush"] = new SolidColorBrush(Color.FromArgb("#1A1035"));
        r["BlueCardBackgroundBrush"]   = new SolidColorBrush(Color.FromArgb("#0F1635"));
        r["InputBackgroundBrush"]      = new SolidColorBrush(Color.FromArgb("#06061A"));
        r["CardBorderBrush"]           = new SolidColorBrush(Color.FromArgb("#2D2D5E"));
        r["AccentCardBorderBrush"]     = new SolidColorBrush(Color.FromArgb("#3D2580"));
        r["BlueCardBorderBrush"]       = new SolidColorBrush(Color.FromArgb("#1E3A80"));
        r["InputBorderBrush"]          = new SolidColorBrush(Color.FromArgb("#3D2A6A"));
        r["EstimateButtonStroke"]      = new SolidColorBrush(Color.FromArgb("#9D5FFF"));
        r["EstimateButtonBrush"]       = EstimateGradient;
    }

    private static void ApplyLight(ResourceDictionary r)
    {
        r["PageBackground"]  = Color.FromArgb("#F4F5FF");
        r["TextPrimary"]     = Color.FromArgb("#1A1035");
        r["TextSecondary"]   = Color.FromArgb("#5B5580");
        r["TextMuted"]       = Color.FromArgb("#8C8AB0");
        r["LabelMuted"]      = Color.FromArgb("#7B7A9E");
        r["LabelSubtitle"]   = Color.FromArgb("#5B5A7A");
        r["AccentLight"]     = Color.FromArgb("#7C3AED");
        r["AccentPrimary"]   = Color.FromArgb("#7C3AED");
        r["AccentBlue"]      = Color.FromArgb("#2563EB");
        r["AccentPink"]      = Color.FromArgb("#DB2777");
        r["ErrorColor"]      = Color.FromArgb("#DB2777");
        r["PlaceholderText"] = Color.FromArgb("#B0AED0");
        r["DividerColor"]    = Color.FromArgb("#DDD8FF");
        r["InputBackground"] = Color.FromArgb("#F8F6FF");
        r["SliderTrack"]     = Color.FromArgb("#DDD8FF");

        r["CardBackgroundBrush"]       = new SolidColorBrush(Color.FromArgb("#FFFFFF"));
        r["CardAltBackgroundBrush"]    = new SolidColorBrush(Color.FromArgb("#F0EDFF"));
        r["AccentCardBackgroundBrush"] = new SolidColorBrush(Color.FromArgb("#EDE8FF"));
        r["BlueCardBackgroundBrush"]   = new SolidColorBrush(Color.FromArgb("#EBF2FF"));
        r["InputBackgroundBrush"]      = new SolidColorBrush(Color.FromArgb("#F8F6FF"));
        r["CardBorderBrush"]           = new SolidColorBrush(Color.FromArgb("#DDD8FF"));
        r["AccentCardBorderBrush"]     = new SolidColorBrush(Color.FromArgb("#A78BFA"));
        r["BlueCardBorderBrush"]       = new SolidColorBrush(Color.FromArgb("#93C5FD"));
        r["InputBorderBrush"]          = new SolidColorBrush(Color.FromArgb("#C4B8FF"));
        r["EstimateButtonStroke"]      = new SolidColorBrush(Color.FromArgb("#A78BFA"));
        r["EstimateButtonBrush"]       = EstimateGradient;
    }

    private static LinearGradientBrush MakeGradient(string from, string to)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(from), Offset = 0f });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(to),   Offset = 1f });
        return brush;
    }
}
