namespace Federation.Protocol;

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
    Task<IReadOnlyList<RoomId>> GetRoomsForDeviceAsync(DeviceId deviceId, CancellationToken ct = default);
}

/// <summary>Manages council rooms.</summary>
public interface IRoomRepository
{
    Task<RoomSummary> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomSummary?> GetRoomAsync(RoomId roomId, CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoomSummary>> ListPublicRoomsAsync(CancellationToken ct = default);
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
    Task RecordSubmissionAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct = default);
    Task<bool> HasSubmittedAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceId>> GetSubmittedDevicesAsync(RoundId roundId, CancellationToken ct = default);
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
    Task<IReadOnlyList<DeviceRegistration>> ListAllAsync(CancellationToken ct = default);
    Task<bool> ValidateTokenAsync(DeviceId deviceId, string token, CancellationToken ct = default);
    Task RemoveDeviceAsync(DeviceId deviceId, CancellationToken ct = default);
}

/// <summary>Creates and validates invite tokens for automatic room membership on registration.</summary>
public interface IInviteTokenRepository
{
    Task<InviteToken> CreateAsync(RoomId roomId, MemberRole role, int maxUses = 1, DateTimeOffset? expiresAt = null, CancellationToken ct = default);
    /// <summary>Validates the token and increments UsedCount. Returns null if invalid/expired/exhausted.</summary>
    Task<InviteToken?> ValidateAndConsumeAsync(string tokenCode, CancellationToken ct = default);
    Task<IReadOnlyList<InviteToken>> ListAsync(CancellationToken ct = default);
    Task<bool> RevokeAsync(string tokenCode, CancellationToken ct = default);
}

/// <summary>Tracks acknowledgement cursors for relay polling.</summary>
public interface IAcknowledgementStore
{
    Task<long> GetCursorAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default);
    Task SetCursorAsync(RoomId roomId, DiscussionId discussionId, long cursor, CancellationToken ct = default);
}

/// <summary>Caches decrypted messages locally, re-encrypted with the device key.</summary>
public interface ILocalMessageCache
{
    Task StoreAsync(MessageId messageId, RoomId roomId, DiscussionId discussionId,
        DeviceId senderDeviceId, MessageType type, int round,
        byte[] contentEncrypted, CancellationToken ct = default);
    Task<IReadOnlyList<CachedMessage>> GetMessagesAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default);
    Task WipeAsync(CancellationToken ct = default);
}

/// <summary>A locally cached message, content re-encrypted with the device key.</summary>
public sealed record CachedMessage
{
    public required MessageId MessageId { get; init; }
    public required DeviceId SenderDeviceId { get; init; }
    public required MessageType Type { get; init; }
    public required int Round { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Encrypted content bytes.")]
    public required byte[] ContentEncrypted { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}
