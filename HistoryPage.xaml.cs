namespace Predict;

public partial class HistoryPage : ContentPage
{
    public HistoryPage(ViewModels.HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ListWrapper.Opacity      = 0;
        ListWrapper.TranslationY = 30;
        await Task.WhenAll(
            ListWrapper.FadeTo(1, 400),
            ListWrapper.TranslateTo(0, 0, 400, Easing.CubicOut));
    }
}
