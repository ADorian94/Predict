using Microsoft.Extensions.Logging;
using Predict.Services;
using Predict.ViewModels;

namespace Predict;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSentry(options =>
            {
                options.Dsn = "https://76fa6d265bfa8411c5e7f0d0146e39db@o4511709623877632.ingest.de.sentry.io/4511709746495568";
                options.TracesSampleRate = 1.0;
#if DEBUG
                options.Debug = true;
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<ResultContext>();

        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ResultViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ResultPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
