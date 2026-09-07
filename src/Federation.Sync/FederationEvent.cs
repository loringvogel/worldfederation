using Federation.Protocol;

namespace Federation.Sync;

/// <summary>Base class for all federation sync events.</summary>
public abstract record FederationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public required string EventType { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ParentEventId { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto signature material.")]
    public required byte[] Signature { get; init; }
}

/// <summary>A membership change event for federation sync.</summary>
public sealed record MembershipChangedEvent : FederationEvent
{
    public required MembershipChangedPayload Payload { get; init; }
}

public sealed record MembershipChangedPayload
{
    public required RoomId RoomId { get; init; }
    public required DeviceId DeviceId { get; init; }
    public required MembershipId MembershipId { get; init; }
}

/// <summary>A round advancement event for federation sync.</summary>
public sealed record RoundAdvancedEvent : FederationEvent
{
    public required RoundAdvancedPayload Payload { get; init; }
}

public sealed record RoundAdvancedPayload
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required DiscussionPhase NewPhase { get; init; }
}

/// <summary>A message accepted event for federation sync.</summary>
public sealed record MessageAcceptedEvent : FederationEvent
{
    public required MessageAcceptedPayload Payload { get; init; }
}

public sealed record MessageAcceptedPayload
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required MessageId MessageId { get; init; }
}

/// <summary>A discussion closed event for federation sync.</summary>
public sealed record DiscussionClosedEvent : FederationEvent
{
    public required DiscussionClosedPayload Payload { get; init; }
}

public sealed record DiscussionClosedPayload
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
}
