namespace Predict;

public partial class ResultPage : ContentPage
{
    public ResultPage(ViewModels.ResultViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as ViewModels.ResultViewModel)?.CancelAnimation();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.WhenAll(
            BestCard.FadeTo(1, 400),
            BestCard.TranslateTo(0, 0, 400, Easing.CubicOut),
            Task.Delay(150).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    FormulasCard.FadeTo(1, 400),
                    FormulasCard.TranslateTo(0, 0, 400, Easing.CubicOut));
            })),
            Task.Delay(300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    PctCard.FadeTo(1, 400),
                    PctCard.TranslateTo(0, 0, 400, Easing.CubicOut));
            }))
        );
    }
}
