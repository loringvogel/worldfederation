using Azure.Data.Tables;
using Federation.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace Federation.Storage.AzureTables;

public static class AzureTablesStorageExtensions
{
    public static IServiceCollection AddAzureTablesStorage(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton(_ => new TableServiceClient(connectionString));

        services.AddSingleton<IDeviceRepository>(sp =>
            new AzureTablesDeviceRepository(sp.GetRequiredService<TableServiceClient>()));

        // Build MembershipRepository and RoomRepository together so we can wire the back-reference
        // that allows MembershipRepository to update the room member count.
        services.AddSingleton<AzureTablesMembershipRepository>(sp =>
            new AzureTablesMembershipRepository(sp.GetRequiredService<TableServiceClient>()));

        services.AddSingleton<IMembershipRepository>(sp =>
            sp.GetRequiredService<AzureTablesMembershipRepository>());

        services.AddSingleton<AzureTablesRoomRepository>(sp =>
        {
            var membershipRepo = sp.GetRequiredService<AzureTablesMembershipRepository>();
            var roomRepo = new AzureTablesRoomRepository(
                sp.GetRequiredService<TableServiceClient>(),
                membershipRepo);
            // Wire back-reference so AddMembershipAsync / RevokeMembershipAsync can update member counts
            membershipRepo.SetRoomRepository(roomRepo);
            return roomRepo;
        });

        services.AddSingleton<IRoomRepository>(sp =>
            sp.GetRequiredService<AzureTablesRoomRepository>());

        services.AddSingleton<IDiscussionRepository>(sp =>
            new AzureTablesDiscussionRepository(sp.GetRequiredService<TableServiceClient>()));

        services.AddSingleton<IMessageStore>(sp =>
            new AzureTablesMessageStore(sp.GetRequiredService<TableServiceClient>()));

        services.AddSingleton<ISecurityEventStore>(sp =>
            new AzureTablesSecurityEventStore(sp.GetRequiredService<TableServiceClient>()));

        return services;
    }
}
