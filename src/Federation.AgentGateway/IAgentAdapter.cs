namespace Federation.AgentGateway;

public interface IAgentAdapter
{
    string ProviderId { get; }
    Task<AgentContribution> DeliberateAsync(
        CouncilContext context,
        AgentPolicy policy,
        CancellationToken cancellationToken);
}
