using Microsoft.Extensions.Logging;

namespace Federation.App.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Settings shared across all pages
        builder.Services.AddSingleton<AppSettingsService>();

        // HttpClient for relay REST calls
        builder.Services.AddHttpClient();

        // Pages
        builder.Services.AddTransient<Pages.SetupPage>();
        builder.Services.AddTransient<Pages.DiscussionPage>();

        return builder.Build();
    }
}
