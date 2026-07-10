namespace Predict;

public partial class MainPage : ContentPage
{
    private bool _hasAppeared;

    public MainPage(ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as ViewModels.MainViewModel)?.RefreshUnit();

        if (_hasAppeared) return;
        _hasAppeared = true;

        WeightCard.Opacity = 0; WeightCard.TranslationY = 40;
        RepsCard.Opacity   = 0; RepsCard.TranslationY   = 40;
        RpeCard.Opacity    = 0; RpeCard.TranslationY    = 40;

        await Task.WhenAll(
            WeightCard.FadeTo(1, 350),
            WeightCard.TranslateTo(0, 0, 350, Easing.CubicOut),
            Task.Delay(120).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    RepsCard.FadeTo(1, 350),
                    RepsCard.TranslateTo(0, 0, 350, Easing.CubicOut));
            })),
            Task.Delay(240).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    RpeCard.FadeTo(1, 350),
                    RpeCard.TranslateTo(0, 0, 350, Easing.CubicOut));
            }))
        );
    }

    private void CalcButton_Clicked(object sender, EventArgs e)
    {
        CalcButton.ScaleTo(0.96, 80).ContinueWith(_ => CalcButton.ScaleTo(1.0, 80));
    }
}
