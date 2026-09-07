namespace Federation.AgentGateway;

/// <summary>Policy constraints for an agent participating in deliberation.</summary>
public sealed record AgentPolicy
{
    public int MaxTokenBudget { get; init; } = 100_000;
    public decimal MaxMonetaryBudgetUsd { get; init; } = 10.00m;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(5);
    public bool AllowExternalLinks { get; init; } // Always false for now
    public bool AllowAttachments { get; init; } // Always false for now

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration record; immutable after init.")]
    public string[] AllowedMessageTypes { get; init; } = ["Proposal", "Critique", "Revision", "Vote", "Synthesis"];
}
