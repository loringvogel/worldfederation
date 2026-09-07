using Federation.Protocol;

namespace Federation.AgentGateway;

/// <summary>The output of an agent's deliberation.</summary>
public sealed record AgentContribution
{
    public required MessageType ContributionType { get; init; }
    public required string Content { get; init; }
    public string? TargetMessageId { get; init; }
}
