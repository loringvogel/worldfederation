using Federation.Protocol;
using Federation.Cryptography;
using Federation.Relay.Routes;
using Federation.Relay.Stores;
using Federation.Relay.Services;
using Federation.Storage;
using Federation.Sync;

var builder = WebApplication.CreateBuilder(args);

var storageMode = builder.Configuration.GetValue<string>("StorageMode") ?? "InMemory";

if (string.Equals(storageMode, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = builder.Configuration.GetValue<string>("DatabasePath") ?? "federation-relay.db";
    builder.Services.AddFederationStorage(dbPath);
}
else
{
    // Register in-memory stores (Phase 1: no external dependencies)
    var membershipRepo = new InMemoryMembershipRepository();
    builder.Services.AddSingleton<IMembershipRepository>(membershipRepo);
    builder.Services.AddSingleton<IRoomRepository>(new InMemoryRoomRepository(membershipRepo));

    var messageStore = new InMemoryMessageStore();
    builder.Services.AddSingleton<IMessageStore>(messageStore);
    builder.Services.AddSingleton(messageStore); // Also register concrete type for test access

    builder.Services.AddSingleton<IDiscussionRepository, InMemoryDiscussionRepository>();
    builder.Services.AddSingleton<ISecurityEventStore, InMemorySecurityEventStore>();
    builder.Services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
}

// Event bus
var eventBus = new InMemoryEventBus();
builder.Services.AddSingleton<IEventBus>(eventBus);
builder.Services.AddSingleton(eventBus);

// Cryptography (Phase 1 in-memory stubs)
builder.Services.AddSingleton<ICryptoProvider, InMemoryCryptoProvider>();

// Sync engine
builder.Services.AddSingleton<IEventLog, InMemoryEventLog>();
builder.Services.AddHostedService<SyncEngine>();

// Round timeout service
builder.Services.AddHostedService(sp =>
    new RoundTimeoutService(
        sp.GetRequiredService<IDiscussionRepository>(),
        sp.GetRequiredService<IEventBus>(),
        sp.GetRequiredService<ILogger<RoundTimeoutService>>()));

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();

// Health check endpoint
app.MapHealthChecks("/health");

// Map all route groups
app.MapDeviceRoutes();
app.MapRoomRoutes();
app.MapMembershipRoutes();
app.MapDiscussionRoutes();
app.MapEnvelopeRoutes();

app.Run();

// Make the implicit Program class accessible for integration tests
public partial class Program;
