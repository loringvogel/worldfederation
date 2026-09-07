// Phase 4: Add Blazor Hybrid pages for room management, discussion viewer, and invitation flow.
using Microsoft.Extensions.Logging;
using Federation.Node;

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

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddLogging(logging => logging.AddDebug());

        // Register federation node services
        builder.Services.AddSingleton<CouncilNodeOptions>(_ => new CouncilNodeOptions
        {
            RelayUrls = [],
            DeviceDisplayName = "MAUI Node",
            PollInterval = TimeSpan.FromSeconds(10),
        });

        return builder.Build();
    }
}
