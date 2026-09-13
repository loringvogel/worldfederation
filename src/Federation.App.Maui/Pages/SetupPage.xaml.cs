using System.Net.Http.Json;

namespace Federation.App.Maui.Pages;

public partial class SetupPage : ContentPage
{
    private readonly AppSettingsService _settings;
    private readonly IHttpClientFactory _http;

    public SetupPage(AppSettingsService settings, IHttpClientFactory http)
    {
        InitializeComponent();
        _settings = settings;
        _http = http;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var s = _settings.Current;
        RelayEntry.Text     = s.RelayUrl;
        AiUrlEntry.Text     = s.AiBaseUrl;
        AiKeyEntry.Text     = s.AiApiKey;
        AiModelEntry.Text   = s.AiModel;
        DeviceNameEntry.Text = s.DeviceName ?? "My-Agent";
        RefreshRegistrationUI(s);
    }

    private void RefreshRegistrationUI(AppSettings s)
    {
        if (s.IsRegistered)
        {
            RegStatusLabel.Text = $"Registered as {s.DeviceName}\nDevice ID: {s.DeviceId}";
            RegStatusLabel.TextColor = Colors.Green;
            DeviceNameEntry.IsEnabled = false;
            RegButton.IsVisible = false;
            UnregButton.IsVisible = true;
        }
        else
        {
            RegStatusLabel.Text = "Not registered.";
            RegStatusLabel.TextColor = Colors.Gray;
            DeviceNameEntry.IsEnabled = true;
            RegButton.IsVisible = true;
            UnregButton.IsVisible = false;
        }
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

            s.DeviceId    = reg.DeviceId;
            s.DeviceToken = reg.DeviceToken;
            s.DeviceName  = name;
            _settings.Apply(s);

            RefreshRegistrationUI(s);
            ShowStatus($"Registered as {name}.", true);
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
        s.DeviceId    = null;
        s.DeviceToken = null;
        s.DeviceName  = null;
        s.ActiveRoomId = null;
        s.ActiveDiscussionId = null;
        _settings.Apply(s);
        RefreshRegistrationUI(s);
        ShowStatus("Device unregistered.", true);
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
        s.RelayUrl  = RelayEntry.Text?.Trim()    ?? s.RelayUrl;
        s.AiBaseUrl = AiUrlEntry.Text?.Trim()    ?? s.AiBaseUrl;
        s.AiApiKey  = AiKeyEntry.Text?.Trim()    ?? s.AiApiKey;
        s.AiModel   = AiModelEntry.Text?.Trim()  ?? s.AiModel;
        _settings.Apply(s);
        ShowStatus("Settings saved.", true);
    }

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
