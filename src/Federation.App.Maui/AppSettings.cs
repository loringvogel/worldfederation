using System.Text.Json.Serialization;

namespace Federation.App.Maui;

/// <summary>
/// Persisted configuration for the local federation node.
///
/// Non-sensitive fields are stored as JSON in the platform app-data directory.
/// Sensitive fields (DeviceToken, AiApiKey) are stored exclusively in the
/// platform secure store — DPAPI on Windows, Keychain on macOS — and are
/// never written to disk in plaintext.
/// </summary>
public sealed class AppSettings
{
    // ── Relay ──────────────────────────────────────────────────────────────
    public string RelayUrl { get; set; } = "http://localhost:5000";

    // ── Device identity ────────────────────────────────────────────────────
    public string? DeviceId   { get; set; }
    public string? DeviceName { get; set; }

    /// <summary>
    /// Auth token for the relay.
    /// NOT serialized to JSON — stored in platform SecureStorage only.
    /// </summary>
    [JsonIgnore]
    public string? DeviceToken { get; set; }

    // ── Active room / discussion ───────────────────────────────────────────
    public string? ActiveRoomId       { get; set; }
    public string? ActiveDiscussionId { get; set; }

    // ── AI provider ───────────────────────────────────────────────────────
    /// <summary>
    /// Base URL for an OpenAI-compatible chat completions endpoint.
    /// Examples:
    ///   Ollama    — http://localhost:11434
    ///   LM Studio — http://localhost:1234
    ///   OpenAI    — https://api.openai.com
    /// </summary>
    public string AiBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// API key.
    /// NOT serialized to JSON — stored in platform SecureStorage only.
    /// Leave empty for local models that don't require a key.
    /// </summary>
    [JsonIgnore]
    public string AiApiKey { get; set; } = string.Empty;

    /// <summary>Model identifier, e.g. "llama3", "gpt-4o", "mistral".</summary>
    public string AiModel { get; set; } = "llama3";

    /// <summary>True once the device has been registered with a relay (DeviceId set).</summary>
    [JsonIgnore]
    public bool IsRegistered => DeviceId is not null;
}
