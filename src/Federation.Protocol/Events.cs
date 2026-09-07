namespace Federation.Protocol;

/// <summary>Base class for all domain events in the council system.</summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A message envelope was accepted and stored by the relay.</summary>
public sealed record MessageAccepted : DomainEvent
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required MessageId MessageId { get; init; }
    public required DeviceId SenderDeviceId { get; init; }
    public required MessageType MessageType { get; init; }
}

/// <summary>A discussion round has advanced to the next phase.</summary>
public sealed record RoundAdvanced : DomainEvent
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required DiscussionPhase PreviousPhase { get; init; }
    public required DiscussionPhase NewPhase { get; init; }
    public required DiscussionTrigger Trigger { get; init; }
}

/// <summary>A member's access to a room has been revoked.</summary>
public sealed record MemberRevoked : DomainEvent
{
    public required RoomId RoomId { get; init; }
    public required MembershipId MembershipId { get; init; }
    public required DeviceId DeviceId { get; init; }
}

/// <summary>A discussion has been closed (completed or cancelled).</summary>
public sealed record DiscussionClosed : DomainEvent
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required DiscussionPhase FinalPhase { get; init; }
}

/// <summary>The cryptographic epoch was rotated (e.g. after member removal).</summary>
public sealed record EpochRotated : DomainEvent
{
    public required RoomId RoomId { get; init; }
    public required EpochId PreviousEpoch { get; init; }
    public required EpochId NewEpoch { get; init; }
}

/// <summary>A security-relevant event for audit logging.</summary>
public sealed record SecurityEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public required DateTimeOffset OccurredAt { get; init; }
    public required string EventType { get; init; }
    public required string Description { get; init; }
    public DeviceId? DeviceId { get; init; }
    public RoomId? RoomId { get; init; }
}
