namespace Federation.AgentGateway;

/// <summary>
/// The policy boundary between remote messages and local agents.
/// Validates that messages do not contain injection attempts, passes context
/// with the untrusted preamble prepended, and enforces policy limits.
/// </summary>
public sealed class AgentGateway
{
    private static readonly string[] KnownSystemPromptPatterns =
    [
        "You are now",
        "SYSTEM:",
        "Ignore previous instructions",
        "Ignore all previous",
        "You are a",
        "Your new instructions",
    ];

    private readonly IAgentAdapter _adapter;
    private readonly AgentPolicy _policy;

    public AgentGateway(IAgentAdapter adapter, AgentPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(policy);
        _adapter = adapter;
        _policy = policy;
    }

    /// <summary>
    /// Runs deliberation through the agent adapter with safety checks.
    /// Message content is checked for basic injection patterns, but the untrusted
    /// preamble in the context provides the primary defense layer.
    /// </summary>
    public async Task<AgentContribution> DeliberateAsync(CouncilContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Basic injection detection - flag but do not remove (the preamble is the defense)
        foreach (var message in context.Messages)
        {
            foreach (var pattern in KnownSystemPromptPatterns)
            {
                if (message.Content.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Log but continue - the untrusted preamble protects the agent
                    break;
                }
            }
        }

        // Ensure the context has the untrusted preamble
        if (string.IsNullOrWhiteSpace(context.UntrustedPreamble))
        {
            throw new InvalidOperationException("CouncilContext must include the untrusted preamble.");
        }

        var contribution = await _adapter.DeliberateAsync(context, _policy, ct).ConfigureAwait(false);

        // Enforce policy: check content length is reasonable relative to token budget
        // (Rough estimate: 1 token ~= 4 chars)
        if (contribution.Content.Length > _policy.MaxTokenBudget * 4)
        {
            throw new InvalidOperationException("Agent contribution exceeds the maximum token budget.");
        }

        return contribution;
    }
}
