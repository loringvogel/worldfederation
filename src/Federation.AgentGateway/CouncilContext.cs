using Federation.Protocol;

namespace Federation.AgentGateway;

/// <summary>Context passed to an agent adapter for deliberation.</summary>
public sealed record CouncilContext
{
    public required RoomId RoomId { get; init; }
    public required DiscussionId DiscussionId { get; init; }
    public required DiscussionPhase CurrentPhase { get; init; }
    public required IReadOnlyList<DecryptedMessage> Messages { get; init; }

    /// <summary>The fixed injection-defense text from the spec.</summary>
    public required string UntrustedPreamble { get; init; }
}
