namespace Federation.Protocol;

/// <summary>
/// Encrypted message envelope as transmitted through the relay.
/// The relay never sees plaintext; it stores and forwards only ciphertext.
/// </summary>
public sealed record MessageEnvelope
{
    public required string ProtocolVersion { get; init; }
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required EpochId Epoch { get; init; }
    public required MessageId MessageId { get; init; }
    public required DeviceId SenderDeviceId { get; init; }
    public required long SenderSequence { get; init; }
    public required MessageType MessageType { get; init; }
    public required int Round { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string CipherSuite { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO for wire serialization of binary crypto data.")]
    public required byte[] Ciphertext { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO for wire serialization of binary crypto data.")]
    public required byte[] Signature { get; init; }
}

/// <summary>Summary view of a room, safe to return from the relay API.</summary>
public sealed record RoomSummary
{
    public required RoomId RoomId { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required int MemberCount { get; init; }
    public required int ActiveDiscussionCount { get; init; }
}

/// <summary>Current state of a discussion, including phase and round tracking.</summary>
public sealed record DiscussionState
{
    public required DiscussionId DiscussionId { get; init; }
    public required RoomId RoomId { get; init; }
    public required string Topic { get; init; }
    public required DiscussionPhase Phase { get; init; }
    public required int CurrentRound { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CurrentDeadline { get; init; }
    public required int TotalSubmissions { get; init; }
    public required int ExpectedSubmissions { get; init; }
}

/// <summary>Tracks state of an individual round within a discussion.</summary>
public sealed record RoundState
{
    public required RoundId RoundId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required int RoundNumber { get; init; }
    public required DiscussionPhase Phase { get; init; }
    public required DateTimeOffset Deadline { get; init; }
    public required IReadOnlyList<DeviceId> SubmittedDevices { get; init; }
    public required IReadOnlyList<DeviceId> ExpectedDevices { get; init; }
}

/// <summary>A membership record linking a device to a room with a specific role.</summary>
public sealed record MembershipRecord
{
    public required MembershipId MembershipId { get; init; }
    public required RoomId RoomId { get; init; }
    public required DeviceId DeviceId { get; init; }
    public required MemberRole Role { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public bool IsActive => RevokedAt is null;
}

/// <summary>Registration information for a device in the system.</summary>
public sealed record DeviceRegistration
{
    public required DeviceId DeviceId { get; init; }
    public required string DisplayName { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO for wire serialization of binary crypto data.")]
    public required byte[] PublicKey { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
    /// <summary>Phase 1 only: simple token for local simulation auth.</summary>
    public required string DeviceToken { get; init; }
}

/// <summary>Request to submit an encrypted envelope to the relay.</summary>
public sealed record SubmitEnvelopeRequest
{
    public required MessageEnvelope Envelope { get; init; }
}

/// <summary>Response containing envelopes retrieved from the relay.</summary>
public sealed record GetEnvelopesResponse
{
    public required IReadOnlyList<MessageEnvelope> Envelopes { get; init; }
    public required long Cursor { get; init; }
}

/// <summary>Request to create a new council room.</summary>
public sealed record CreateRoomRequest
{
    public required string Name { get; init; }
    public required DeviceId OwnerDeviceId { get; init; }
}

/// <summary>Request to create a new discussion within a room.</summary>
public sealed record CreateDiscussionRequest
{
    public required RoomId RoomId { get; init; }
    public required string Topic { get; init; }
    public required DeviceId InitiatorDeviceId { get; init; }
}
