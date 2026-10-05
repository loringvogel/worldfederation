using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;

namespace Federation.App.Maui.Pages;

public partial class SetupPage : ContentPage
{
    private readonly AppSettingsService _settings;
    private readonly IHttpClientFactory _http;

    private static readonly string FederationBinDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".federation", "bin");

    private static readonly string NodeConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".federation", "federation-node.json");

    private static string McpExePath =>
        Path.Combine(FederationBinDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "federation-mcp.exe" : "federation-mcp");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SetupPage(AppSettingsService settings, IHttpClientFactory http)
    {
        InitializeComponent();
        _settings = settings;
        _http = http;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Ensure secrets are loaded from DPAPI / Keychain before populating the form.
        await _settings.InitializeAsync().ConfigureAwait(true);

        var s = _settings.Current;
        RelayEntry.Text      = s.RelayUrl;
        AiUrlEntry.Text      = s.AiBaseUrl;
        AiKeyEntry.Text      = s.AiApiKey;
        AiModelEntry.Text    = s.AiModel;
        DeviceNameEntry.Text = s.DeviceName ?? "My-Agent";
        RefreshRegistrationUI(s);
    }

    private void RefreshRegistrationUI(AppSettings s)
    {
        if (s.IsRegistered)
        {
            RegStatusLabel.Text      = $"Registered as {s.DeviceName}";
            RegStatusLabel.TextColor = Colors.Green;
            DeviceNameEntry.IsEnabled = false;
            RegButton.IsVisible   = false;
            UnregButton.IsVisible = true;

            DeviceIdLabel.Text      = s.DeviceId;
            DeviceIdSection.IsVisible   = true;
            ClaudeCodeSection.IsVisible = true;
        }
        else
        {
            RegStatusLabel.Text      = "Not registered.";
            RegStatusLabel.TextColor = Colors.Gray;
            DeviceNameEntry.IsEnabled = true;
            RegButton.IsVisible   = true;
            UnregButton.IsVisible = false;

            DeviceIdSection.IsVisible   = false;
            ClaudeCodeSection.IsVisible = false;
        }
    }

    private void OnCopyDeviceId(object? sender, EventArgs e)
    {
        var id = _settings.Current.DeviceId;
        if (string.IsNullOrEmpty(id)) return;
        Clipboard.SetTextAsync(id);
        ShowStatus("Device ID copied to clipboard.", true);
    }

    private void OnBrowseProjectFolder(object? sender, EventArgs e)
    {
        // FolderPicker is Windows/Mac only — handled via platform-specific API
        _ = PickFolderAsync();
    }

    private async Task PickFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(default);
            if (result.IsSuccessful)
            {
                ProjectFolderEntry.Text = result.Folder.Path;
                SetupClaudeButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open folder picker: {ex.Message}", false);
        }
    }

    private async void OnSetupClaudeCodeAsync(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        if (!s.IsRegistered) { ShowStatus("Register first.", false); return; }

        var projectFolder = ProjectFolderEntry.Text?.Trim();
        if (string.IsNullOrEmpty(projectFolder)) { ShowStatus("Choose a project folder first.", false); return; }

        SetupClaudeButton.IsEnabled = false;
        SetupProgressLabel.IsVisible = true;

        try
        {
            // 1. Download federation-mcp if not already installed
            if (!File.Exists(McpExePath))
            {
                SetupProgressLabel.Text = "Downloading federation-mcp…";
                await DownloadMcpBinaryAsync().ConfigureAwait(true);
            }

            // 2. Write node config to ~/.federation/federation-node.json
            SetupProgressLabel.Text = "Writing node config…";
            WriteNodeConfig(s);

            // 3. Write .mcp.json to project folder
            SetupProgressLabel.Text = "Writing Claude Code config…";
            WriteMcpJson(projectFolder);

            SetupProgressLabel.IsVisible = false;
            ShowStatus($"Done! Restart Claude Code in that folder.\n\nYour Device ID:\n{s.DeviceId}\n\nShare it with your room owner to be invited.", true);
        }
        catch (Exception ex)
        {
            SetupProgressLabel.IsVisible = false;
            ShowStatus($"Setup failed: {ex.Message}", false);
        }
        finally
        {
            SetupClaudeButton.IsEnabled = true;
        }
    }

    private async Task DownloadMcpBinaryAsync()
    {
        var rid = GetRid();
        var latestJson = await _http.CreateClient()
            .GetStringAsync(new Uri("https://api.github.com/repos/loringvogel/worldfederation/releases/latest"))
            .ConfigureAwait(true);
        using var doc = JsonDocument.Parse(latestJson);
        var tag = doc.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("Could not determine latest release.");

        var zipUrl = $"https://github.com/loringvogel/worldfederation/releases/download/{tag}/federation-mcp-{rid}.zip";

        using var resp = await _http.CreateClient().GetAsync(new Uri(zipUrl)).ConfigureAwait(true);
        resp.EnsureSuccessStatusCode();

        Directory.CreateDirectory(FederationBinDir);

        var zipBytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(true);
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var dest = Path.Combine(FederationBinDir, entry.Name);
            entry.ExtractToFile(dest, overwrite: true);
        }

        // Make executable on macOS/Linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            System.Diagnostics.Process.Start("chmod", $"+x \"{McpExePath}\"")?.WaitForExit();
    }

    private static void WriteNodeConfig(AppSettings s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NodeConfigPath)!);

        var config = new
        {
            RelayUrl    = s.RelayUrl,
            DeviceName  = s.DeviceName,
            DeviceId    = s.DeviceId,
            DeviceToken = s.DeviceToken,
            Rooms       = new Dictionary<string, string>(),
            ActiveRoomId       = (string?)null,
            ActiveDiscussionId = (string?)null,
            SenderSequences    = new Dictionary<string, long>(),
        };

        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(NodeConfigPath, json);
    }

    private static void WriteMcpJson(string projectFolder)
    {
        var mcpJson = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["federation"] = new
                {
                    command = McpExePath,
                    args    = new[] { "--config", NodeConfigPath },
                }
            }
        };

        var json = JsonSerializer.Serialize(mcpJson, JsonOpts);
        File.WriteAllText(Path.Combine(projectFolder, ".mcp.json"), json);
    }

    private static string GetRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }

    private async void OnTestRelayAsync(object? sender, EventArgs e)
    {
        var url = RelayEntry.Text?.Trim();
        if (string.IsNullOrEmpty(url)) { ShowStatus("Enter relay URL first.", false); return; }
        try
        {
            using var client = _http.CreateClient();
            var resp = await client.GetAsync(new Uri(url.TrimEnd('/') + "/health"));
            ShowStatus(resp.IsSuccessStatusCode
                ? "Relay reachable."
                : $"Relay returned HTTP {(int)resp.StatusCode}.", resp.IsSuccessStatusCode);
        }
        catch (Exception ex)
        {
            ShowStatus($"Cannot reach relay: {ex.Message}", false);
        }
    }

    private async void OnRegisterAsync(object? sender, EventArgs e)
    {
        var relay = RelayEntry.Text?.Trim();
        var name  = DeviceNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(relay)) { ShowStatus("Enter relay URL first.", false); return; }
        if (string.IsNullOrEmpty(name))  { ShowStatus("Enter a device name.", false); return; }

        RegButton.IsEnabled = false;
        try
        {
            var s = _settings.Current;
            s.RelayUrl = relay;
            _settings.Apply(s);

            using var client = _http.CreateClient();
            var payload = new { DisplayName = name };
            var resp = await client.PostAsJsonAsync(new Uri(relay.TrimEnd('/') + "/v1/devices/registrations"), payload);
            if (!resp.IsSuccessStatusCode)
            {
                ShowStatus($"Registration failed (HTTP {(int)resp.StatusCode}).", false);
                return;
            }

            var reg = await resp.Content.ReadFromJsonAsync<DeviceRegResponse>();
            if (reg is null) { ShowStatus("Invalid relay response.", false); return; }

            // DeviceToken is a secret — Apply() stores it in DPAPI/Keychain, not JSON.
            s.DeviceId    = reg.DeviceId;
            s.DeviceToken = reg.DeviceToken;
            s.DeviceName  = name;
            _settings.Apply(s);

            RefreshRegistrationUI(s);
            ShowStatus($"Registered as {name}. Token secured in {SecureStorePlatformName()}.", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
        }
        finally
        {
            RegButton.IsEnabled = true;
        }
    }

    private void OnUnregister(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        s.DeviceId           = null;
        s.DeviceToken        = null;
        s.DeviceName         = null;
        s.ActiveRoomId       = null;
        s.ActiveDiscussionId = null;
        _settings.Apply(s);

        // Remove secrets from the platform secure store.
        AppSettingsService.ClearSecrets();

        RefreshRegistrationUI(s);
        ProjectFolderEntry.Text = string.Empty;
        SetupProgressLabel.IsVisible = false;
        ShowStatus("Device unregistered. Secrets cleared from secure store.", true);
    }

    private void OnPresetChanged(object? sender, EventArgs e)
    {
        switch (ProviderPicker.SelectedIndex)
        {
            case 0: // Ollama
                AiUrlEntry.Text   = "http://localhost:11434";
                AiKeyEntry.Text   = string.Empty;
                AiModelEntry.Text = "llama3";
                break;
            case 1: // LM Studio
                AiUrlEntry.Text   = "http://localhost:1234";
                AiKeyEntry.Text   = string.Empty;
                AiModelEntry.Text = "local-model";
                break;
            case 2: // OpenAI
                AiUrlEntry.Text   = "https://api.openai.com";
                AiModelEntry.Text = "gpt-4o";
                break;
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        s.RelayUrl  = RelayEntry.Text?.Trim()   ?? s.RelayUrl;
        s.AiBaseUrl = AiUrlEntry.Text?.Trim()   ?? s.AiBaseUrl;
        s.AiApiKey  = AiKeyEntry.Text?.Trim()   ?? s.AiApiKey;   // goes to Keychain/DPAPI
        s.AiModel   = AiModelEntry.Text?.Trim() ?? s.AiModel;
        _settings.Apply(s);
        ShowStatus($"Settings saved. API key secured in {SecureStorePlatformName()}.", true);
    }

    private static string SecureStorePlatformName() =>
#if WINDOWS
        "Windows DPAPI";
#elif MACCATALYST
        "macOS Keychain";
#else
        "secure storage";
#endif

    private void ShowStatus(string message, bool success)
    {
        StatusLabel.Text = message;
        StatusBanner.BackgroundColor = success
            ? Color.FromArgb("#d1f7dd")
            : Color.FromArgb("#fde8e8");
        StatusLabel.TextColor = success
            ? Color.FromArgb("#1a6631")
            : Color.FromArgb("#9b1111");
        StatusBanner.IsVisible = true;
    }

    private sealed class DeviceRegResponse
    {
        public string DeviceId    { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
    }
}
