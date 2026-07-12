namespace Predict.Services;

public class AppSettingsService
{
    public bool IsLbs
    {
        get => Preferences.Default.Get(AppConstants.PrefIsLbs, false);
        set => Preferences.Default.Set(AppConstants.PrefIsLbs, value);
    }

    public bool IsRounded
    {
        get => Preferences.Default.Get(AppConstants.PrefIsRounded, true);
        set => Preferences.Default.Set(AppConstants.PrefIsRounded, value);
    }

    public string Theme
    {
        get
        {
            var saved = Preferences.Default.Get(AppConstants.PrefTheme, AppConstants.ThemeDark);
            // Guard against stale values from older app versions that had more themes
            return saved == AppConstants.ThemeLight ? saved : AppConstants.ThemeDark;
        }
        set => Preferences.Default.Set(AppConstants.PrefTheme, value);
    }
}
