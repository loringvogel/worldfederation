namespace Federation.AgentAdapters.OpenAI;

/// <summary>Configuration options for the OpenAI agent adapter.</summary>
public sealed record OpenAiAdapterOptions
{
    // API key must never be logged, shared with relays, peers, or other council members.
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4o";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Kept as string for simple concatenation.")]
    public string BaseUrl { get; init; } = "https://api.openai.com";
}
