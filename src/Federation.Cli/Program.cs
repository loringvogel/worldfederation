using Federation.Cryptography;
using Federation.Node;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Federation.Cli;

public static class Program
{
    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var relayUrl = GetArg(args, "--relay-url") ?? "http://localhost:5000";
        var deviceName = GetArg(args, "--device-name") ?? "CLI Node";

        var crypto = new InMemoryCryptoProvider();
        var (publicKey, privateKey) = crypto.GenerateDeviceKeyPair();

        var options = new CouncilNodeOptions
        {
            RelayBaseUrl = relayUrl,
            DeviceId = Federation.Protocol.DeviceId.New(),
            DeviceToken = Guid.NewGuid().ToString("N"),
            RoomIds = [],
            PollInterval = TimeSpan.FromSeconds(5),
            RelayUrls = string.IsNullOrEmpty(relayUrl) ? [] : [relayUrl],
        };

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<ICryptoProvider>(crypto);
                services.AddSingleton<IGroupSession>(new InMemoryGroupSession());
                services.AddSingleton<IKeyStore>(new InMemoryKeyStore());
                services.AddSingleton<InMemoryLocalCache>();
                services.AddHttpClient();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Federation.Cli");
        logger.LogInformation("Federation CLI Node starting. Device: {DeviceId}, Relay: {RelayUrl}", options.DeviceId, relayUrl);
        logger.LogInformation("Device name: {DeviceName}", deviceName);
        logger.LogInformation("Commands: list-rooms, stop");

        using var cts = new CancellationTokenSource();

        // Start host in background
        _ = host.RunAsync(cts.Token);

        // Read commands from stdin
        await Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var line = Console.ReadLine();
                if (line is null)
                {
                    break;
                }

                var command = line.Trim().ToUpperInvariant();
                switch (command)
                {
                    case "LIST-ROOMS":
                        logger.LogInformation("Room listing requires an active relay connection. Rooms configured: {Count}", options.RoomIds.Length);
                        foreach (var roomId in options.RoomIds)
                        {
                            Console.WriteLine($"  Room: {roomId}");
                        }
                        break;

                    case "STOP":
                        logger.LogInformation("Shutting down...");
                        await cts.CancelAsync().ConfigureAwait(false);
                        break;

                    case "":
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Console.WriteLine("Available commands: list-rooms, stop");
                        break;
                }
            }
        }, cts.Token).ConfigureAwait(false);

        logger.LogInformation("Federation CLI Node stopped.");
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
