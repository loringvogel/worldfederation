using Federation.Identity;
using Federation.Protocol;
using Federation.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace Federation.Storage;

/// <summary>Registration extensions for Federation.Storage services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all SQLite-backed Federation.Storage implementations.</summary>
    public static IServiceCollection AddFederationStorage(this IServiceCollection services, string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddSingleton(new FederationDatabase(databasePath));
        services.AddSingleton<IMessageStore, SqliteMessageStore>();
        services.AddSingleton<IRoomRepository, SqliteRoomRepository>();
        services.AddSingleton<IDiscussionRepository, SqliteDiscussionRepository>();
        services.AddSingleton<IMembershipRepository, SqliteMembershipRepository>();
        services.AddSingleton<ISecurityEventStore, SqliteSecurityEventStore>();
        services.AddSingleton<IIdentityStore, SqliteIdentityStore>();
        services.AddSingleton<IEventLog, SqliteEventLog>();

        return services;
    }
}
