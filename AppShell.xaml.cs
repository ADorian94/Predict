namespace Predict;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ResultPage), typeof(ResultPage));
        Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
