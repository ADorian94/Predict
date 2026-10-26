namespace Predict;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(ViewModels.SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.SettingsViewModel vm) vm.RefreshAdvertisingPrivacy();
    }
}
