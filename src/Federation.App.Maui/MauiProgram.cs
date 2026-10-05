using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Federation.App.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Settings shared across all pages
        builder.Services.AddSingleton<AppSettingsService>();

        // HttpClient for relay REST calls + GitHub API (requires User-Agent)
        builder.Services.AddHttpClient(string.Empty, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentFederation/1.0");
        });

        // Pages
        builder.Services.AddTransient<Pages.SetupPage>();
        builder.Services.AddTransient<Pages.DiscussionPage>();
        builder.Services.AddTransient<Pages.AgentsPage>();

        return builder.Build();
    }
}
