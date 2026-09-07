using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Federation.AgentGateway;
using Federation.Protocol;

namespace Federation.AgentAdapters.OpenAI;

/// <summary>
/// Agent adapter that uses the OpenAI chat completions endpoint.
/// Uses only System.Net.Http.HttpClient - no OpenAI SDK NuGet package.
/// </summary>
public sealed class OpenAiAgentAdapter : IAgentAdapter
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiAdapterOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public OpenAiAgentAdapter(HttpClient httpClient, OpenAiAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;

        // API key must never be logged, shared with relays, peers, or other council members.
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public string ProviderId => "openai";

    public async Task<AgentContribution> DeliberateAsync(
        CouncilContext context,
        AgentPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);

        var messages = new List<object>();

        // System message with policy constraints and untrusted preamble
        var systemContent = $"""
            You are a council participant. Follow these policy constraints:
            - Maximum token budget: {policy.MaxTokenBudget}
            - Maximum monetary budget: ${policy.MaxMonetaryBudgetUsd}
            - Maximum duration: {policy.MaxDuration}
            - External links: {(policy.AllowExternalLinks ? "allowed" : "NOT allowed")}
            - Attachments: {(policy.AllowAttachments ? "allowed" : "NOT allowed")}
            - Allowed message types: {string.Join(", ", policy.AllowedMessageTypes)}

            {context.UntrustedPreamble}
            """;

        messages.Add(new { role = "system", content = systemContent });

        // User messages from council peers
        foreach (var msg in context.Messages)
        {
            messages.Add(new { role = "user", content = $"[Device {msg.SenderDeviceId}, {msg.Type}, Round {msg.Round}]: {msg.Content}" });
        }

        var requestBody = new
        {
            model = _options.Model,
            messages,
            max_tokens = policy.MaxTokenBudget,
        };

        var url = $"{_options.BaseUrl}/v1/chat/completions";
        var response = await _httpClient.PostAsJsonAsync(new Uri(url), requestBody, JsonOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var content = responseJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        // Determine contribution type based on current phase
        var contributionType = context.CurrentPhase switch
        {
            DiscussionPhase.ProposalRound => MessageType.Proposal,
            DiscussionPhase.CritiqueRound => MessageType.Critique,
            DiscussionPhase.RevisionRound => MessageType.Revision,
            DiscussionPhase.Vote => MessageType.Vote,
            DiscussionPhase.Synthesis => MessageType.Synthesis,
            _ => MessageType.Proposal,
        };

        return new AgentContribution
        {
            ContributionType = contributionType,
            Content = content,
        };
    }
}
