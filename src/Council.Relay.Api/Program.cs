using Council.Contracts;
using Council.Cryptography;
using Council.Relay.Api.Routes;
using Council.Relay.Api.Stores;

var builder = WebApplication.CreateBuilder(args);

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

// Event bus
var eventBus = new InMemoryEventBus();
builder.Services.AddSingleton<IEventBus>(eventBus);
builder.Services.AddSingleton(eventBus);

// Cryptography (Phase 1 in-memory stubs)
builder.Services.AddSingleton<ICryptoProvider, InMemoryCryptoProvider>();

var app = builder.Build();

// Map all route groups
app.MapDeviceRoutes();
app.MapRoomRoutes();
app.MapMembershipRoutes();
app.MapDiscussionRoutes();
app.MapEnvelopeRoutes();

app.Run();

// Make the implicit Program class accessible for integration tests
public partial class Program;
