namespace Federation.AgentGateway;

/// <summary>
/// Adapter for human participation. Throws NotSupportedException since humans
/// provide input through the CouncilToolService, not through automated deliberation.
/// </summary>
public sealed class HumanOnlyAdapter : IAgentAdapter
{
    public string ProviderId => "human";

    public Task<AgentContribution> DeliberateAsync(
        CouncilContext context,
        AgentPolicy policy,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "Human participation requires direct input. Use CouncilToolService to submit contributions.");
    }
}
