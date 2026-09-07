using Council.Coordinator.Worker;

var builder = Host.CreateApplicationBuilder(args);

// In Phase 1, this worker is meant to be hosted alongside the Relay.Api
// via builder.Services.AddHostedService<RoundTimeoutService>() in the relay's startup.
// This standalone host is provided for cases where you want to run the coordinator separately.

builder.Services.AddHostedService(sp =>
    new RoundTimeoutService(
        sp.GetRequiredService<Council.Contracts.IDiscussionRepository>(),
        sp.GetRequiredService<Council.Contracts.IEventBus>(),
        sp.GetRequiredService<ILogger<RoundTimeoutService>>()));

var host = builder.Build();
host.Run();
