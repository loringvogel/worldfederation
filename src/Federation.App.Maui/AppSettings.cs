using System.Text.Json.Serialization;

namespace Federation.App.Maui;

/// <summary>Persisted configuration for the local federation node.</summary>
public sealed class AppSettings
{
    // ── Relay ──────────────────────────────────────────────────────────────
    public string RelayUrl { get; set; } = "http://localhost:5000";

    // ── Device identity (populated after registration) ─────────────────────
    public string? DeviceId { get; set; }
    public string? DeviceToken { get; set; }
    public string? DeviceName { get; set; }

    // ── Active room / discussion ───────────────────────────────────────────
    public string? ActiveRoomId { get; set; }
    public string? ActiveDiscussionId { get; set; }

    // ── AI provider ───────────────────────────────────────────────────────
    /// <summary>
    /// Base URL for an OpenAI-compatible chat completions endpoint.
    /// Examples:
    ///   OpenAI   — https://api.openai.com
    ///   Ollama   — http://localhost:11434
    ///   LM Studio — http://localhost:1234
    ///   Anthropic — https://api.anthropic.com/v1 (with compatible wrapper)
    /// </summary>
    public string AiBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>API key. Leave blank for local models that don't require one.</summary>
    public string AiApiKey { get; set; } = string.Empty;

    /// <summary>Model identifier, e.g. "llama3", "gpt-4o", "mistral".</summary>
    public string AiModel { get; set; } = "llama3";

    [JsonIgnore]
    public bool IsRegistered => DeviceId is not null && DeviceToken is not null;
}
