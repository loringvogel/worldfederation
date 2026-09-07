using Council.Contracts;

namespace Council.Node;

/// <summary>Configuration for a local council node.</summary>
public sealed record CouncilNodeOptions
{
    /// <summary>Base URL of the relay API.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Kept as string for simple concatenation in Phase 1.")]
    public required string RelayBaseUrl { get; init; }

    /// <summary>This node's device identifier.</summary>
    public required DeviceId DeviceId { get; init; }

    /// <summary>Phase 1 auth token for the relay.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Room IDs this node participates in.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration record; immutable after init.")]
    public required RoomId[] RoomIds { get; init; }

    /// <summary>How often to poll the relay for new envelopes.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
}
