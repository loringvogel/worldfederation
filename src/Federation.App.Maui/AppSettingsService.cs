using System.Text.Json;

namespace Federation.App.Maui;

/// <summary>
/// Loads and saves AppSettings with two-tier storage:
///
///   Tier 1 — JSON file in the platform app-data directory.
///             Contains non-sensitive fields only (relay URL, device ID, active room, AI model, etc.).
///
///   Tier 2 — Platform secure store (DPAPI on Windows, Keychain on macOS).
///             Contains secrets that must never appear on disk in plaintext:
///               • DeviceToken  — relay auth token
///               • AiApiKey     — AI provider API key
///
/// Call InitializeAsync() once at app startup to populate secrets from the secure store.
/// </summary>
public sealed class AppSettingsService
{
    private const string KeyDeviceToken = "fed.device_token";
    private const string KeyAiApiKey    = "fed.ai_api_key";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented            = true,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _jsonPath;
    private AppSettings _current;
    private bool _secretsLoaded;

    public AppSettingsService()
    {
        _jsonPath = Path.Combine(FileSystem.AppDataDirectory, "federation-node.json");
        _current  = LoadJson();
    }

    public AppSettings Current => _current;

    /// <summary>
    /// Merges secrets from the platform secure store into Current.
    /// Idempotent — safe to call multiple times; only fetches once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_secretsLoaded) return;
        try
        {
            _current.DeviceToken = await SecureStorage.GetAsync(KeyDeviceToken).ConfigureAwait(false);
            _current.AiApiKey    = await SecureStorage.GetAsync(KeyAiApiKey).ConfigureAwait(false) ?? string.Empty;
        }
        catch
        {
            // SecureStorage unavailable on this platform/config — secrets remain in-memory only.
        }
        _secretsLoaded = true;
    }

    /// <summary>Saves the current settings (JSON + SecureStorage).</summary>
    public void Save() => ApplyInternal(_current);

    /// <summary>Replaces current settings with the supplied instance and saves.</summary>
    public void Apply(AppSettings updated) => ApplyInternal(updated);

    private void ApplyInternal(AppSettings s)
    {
        _current = s;
        WriteJson(s);
        _ = WriteSecretsAsync(s); // fire-and-forget; fast, non-blocking for the UI
    }

    private void WriteJson(AppSettings s)
    {
        // Write only non-sensitive fields. DeviceToken and AiApiKey are [JsonIgnore]
        // so they are excluded automatically from serialization.
        var json = JsonSerializer.Serialize(s, JsonOpts);
        File.WriteAllText(_jsonPath, json);
    }

    private static async Task WriteSecretsAsync(AppSettings s)
    {
        try
        {
            if (s.DeviceToken is not null)
                await SecureStorage.SetAsync(KeyDeviceToken, s.DeviceToken).ConfigureAwait(false);
            else
                SecureStorage.Remove(KeyDeviceToken);

            await SecureStorage.SetAsync(KeyAiApiKey, s.AiApiKey).ConfigureAwait(false);
        }
        catch
        {
            // SecureStorage unavailable — secrets held in-memory for this session only.
        }
    }

    /// <summary>
    /// Removes all secrets from the platform secure store.
    /// Call when the user unregisters their device.
    /// </summary>
    public static void ClearSecrets()
    {
        try
        {
            SecureStorage.Remove(KeyDeviceToken);
            SecureStorage.Remove(KeyAiApiKey);
        }
        catch { /* ignore — already absent or unavailable */ }
    }

    private AppSettings LoadJson()
    {
        try
        {
            if (File.Exists(_jsonPath))
            {
                var json = File.ReadAllText(_jsonPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { /* first run or corrupt file — start fresh */ }
        return new AppSettings();
    }
}
