namespace Predict;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        base.OnStart();
        Predict.Services.MixpanelService.Track("app_opened");
        CheckCrashLog();
    }

    private static async void CheckCrashLog()
    {
#if ANDROID
        var path = MainApplication.CrashLogPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var log = File.ReadAllText(path);
            File.Delete(path);
            await Task.Delay(1000);
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert("Crash Log", log, "OK");
        }
#endif
    }
}
