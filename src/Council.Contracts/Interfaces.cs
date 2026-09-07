namespace Council.Contracts;

/// <summary>
/// Stores encrypted message envelopes. The relay MUST only store ciphertext;
/// no plaintext content may be persisted through this interface.
/// </summary>
public interface IMessageStore
{
    Task<long> StoreEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct = default);
    Task<GetEnvelopesResponse> GetEnvelopesAsync(RoomId roomId, DiscussionId discussionId, long afterCursor, CancellationToken ct = default);
    Task<bool> ExistsAsync(MessageId messageId, CancellationToken ct = default);
}

/// <summary>Publishes domain events to interested subscribers.</summary>
public interface IEventBus
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>Manages room memberships and device lookups.</summary>
public interface IMembershipRepository
{
    Task<MembershipRecord?> GetMembershipAsync(MembershipId membershipId, CancellationToken ct = default);
    Task<MembershipRecord?> GetMembershipByDeviceAsync(RoomId roomId, DeviceId deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<MembershipRecord>> GetActiveMembershipsAsync(RoomId roomId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceId>> GetDevicesForRoomAsync(RoomId roomId, CancellationToken ct = default);
    Task<MembershipRecord> AddMembershipAsync(RoomId roomId, DeviceId deviceId, MemberRole role, CancellationToken ct = default);
    Task RevokeMembershipAsync(MembershipId membershipId, CancellationToken ct = default);
}

/// <summary>Manages council rooms.</summary>
public interface IRoomRepository
{
    Task<RoomSummary> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomSummary?> GetRoomAsync(RoomId roomId, CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct = default);
}

/// <summary>Manages discussions within rooms.</summary>
public interface IDiscussionRepository
{
    Task<DiscussionState> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default);
    Task<DiscussionState?> GetDiscussionAsync(DiscussionId discussionId, CancellationToken ct = default);
    Task UpdateDiscussionStateAsync(DiscussionId discussionId, DiscussionPhase newPhase, int newRound, DateTimeOffset? newDeadline, CancellationToken ct = default);
    Task IncrementSubmissionCountAsync(DiscussionId discussionId, CancellationToken ct = default);
    Task<IReadOnlyList<DiscussionState>> GetActiveDiscussionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DiscussionState>> GetDiscussionsInRoomAsync(RoomId roomId, CancellationToken ct = default);
}

/// <summary>Stores security-relevant events for audit.</summary>
public interface ISecurityEventStore
{
    Task AppendAsync(SecurityEvent securityEvent, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityEvent>> GetEventsAsync(RoomId? roomId, int limit = 100, CancellationToken ct = default);
}

/// <summary>Manages device registrations.</summary>
public interface IDeviceRepository
{
    Task<DeviceRegistration> RegisterDeviceAsync(string displayName, byte[] publicKey, CancellationToken ct = default);
    Task<DeviceRegistration?> GetDeviceAsync(DeviceId deviceId, CancellationToken ct = default);
    Task<bool> ValidateTokenAsync(DeviceId deviceId, string token, CancellationToken ct = default);
    Task RemoveDeviceAsync(DeviceId deviceId, CancellationToken ct = default);
}
