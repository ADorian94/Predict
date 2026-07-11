namespace Predict.Services;

public static class ThemeService
{
    private static string _current = AppConstants.ThemeDark;
    public static string Current => _current;

    // Color accessors for ViewModel use — always reflect active theme
    public static Color CardBg        => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#13132B");
    public static Color CardAltBg     => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#F0EDFF") : Color.FromArgb("#1E1E3A");
    public static Color AccentCardBg  => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#EDE8FF") : Color.FromArgb("#1A1035");
    public static Color CardBorderCol => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#DDD8FF") : Color.FromArgb("#2D2D5E");
    public static Color AccentLight   => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#7C3AED") : Color.FromArgb("#C084FC");
    public static Color MutedText     => _current == AppConstants.ThemeLight
        ? Color.FromArgb("#8C8AB0") : Color.FromArgb("#505870");

    public static void Apply(string theme)
    {
        _current = theme;
        var r = Application.Current!.Resources;

        if (theme == AppConstants.ThemeLight)
            ApplyLight(r);
        else
            ApplyDark(r);
    }

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
        r["EstimateButtonBrush"]       = MakeGradient("#7C3AED", "#2563EB");
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
        r["EstimateButtonBrush"]       = MakeGradient("#7C3AED", "#2563EB");
    }

    private static LinearGradientBrush MakeGradient(string from, string to)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(1, 0),
        };
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(from), Offset = 0f });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(to),   Offset = 1f });
        return brush;
    }
}
