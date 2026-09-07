// Azure adapters (PostgreSQL, Service Bus, Blob Storage) implementing:
//   - IMessageStore
//   - IRoomRepository
//   - IDiscussionRepository
//   - IMembershipRepository
//   - ISecurityEventStore
//   - IEventBus
// from Federation.Protocol.
//
// Phase 2+: Add Azure SDK references here and implement concrete adapters.
// This project is the ONLY project that should reference Azure SDKs.
//
// Planned adapters:
//   - PostgresMessageStore : IMessageStore
//   - PostgresRoomRepository : IRoomRepository
//   - PostgresDiscussionRepository : IDiscussionRepository
//   - PostgresMembershipRepository : IMembershipRepository
//   - PostgresSecurityEventStore : ISecurityEventStore
//   - ServiceBusEventBus : IEventBus
//   - BlobStorageKeyStore (for encrypted key backup)

namespace Federation.Infrastructure;

/// <summary>
/// Marker class for the infrastructure assembly. No implementations in Phase 1.
/// </summary>
public static class InfrastructureAssemblyMarker
{
    /// <summary>Assembly name constant for discovery.</summary>
    public const string AssemblyName = "Federation.Infrastructure";
}
