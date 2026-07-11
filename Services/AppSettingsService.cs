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
            var v = Preferences.Default.Get(AppConstants.PrefTheme, AppConstants.ThemeDark);
            return v == AppConstants.ThemeLight ? v : AppConstants.ThemeDark;
        }
        set => Preferences.Default.Set(AppConstants.PrefTheme, value);
    }
}
