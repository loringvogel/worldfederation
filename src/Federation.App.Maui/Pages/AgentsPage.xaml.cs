using System.Net.Http.Json;
using System.Text.Json;

namespace Federation.App.Maui.Pages;

public partial class AgentsPage : ContentPage
{
    private readonly AppSettingsService _settings;
    private readonly IHttpClientFactory _http;
    private string _lastTokenCommand = string.Empty;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public AgentsPage(AppSettingsService settings, IHttpClientFactory http)
    {
        InitializeComponent();
        _settings = settings;
        _http = http;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRoomsAsync().ConfigureAwait(true);
        await RefreshAllAsync().ConfigureAwait(true);
    }

    private async Task LoadRoomsAsync()
    {
        var s = _settings.Current;
        if (!s.IsRegistered) return;
        try
        {
            using var client = MakeClient(s);
            var rooms = await client.GetFromJsonAsync<List<RoomDto>>($"{s.RelayUrl.TrimEnd('/')}/v1/rooms", JsonOpts).ConfigureAwait(true);
            InviteRoomPicker.Items.Clear();
            if (rooms is null) return;
            foreach (var r in rooms)
                InviteRoomPicker.Items.Add($"{r.Name} ({r.RoomId[..8]}…)");
            InviteRoomPicker.BindingContext = rooms;
            if (rooms.Count > 0) InviteRoomPicker.SelectedIndex = 0;
        }
        catch { /* ignore */ }
    }

    private async void OnRefreshAsync(object? sender, EventArgs e) =>
        await RefreshAllAsync().ConfigureAwait(true);

    private async Task RefreshAllAsync()
    {
        var s = _settings.Current;
        if (!s.IsRegistered) return;
        using var client = MakeClient(s);
        var relay = s.RelayUrl.TrimEnd('/');
        try
        {
            var devices = await client.GetFromJsonAsync<List<DeviceDto>>($"{relay}/v1/devices", JsonOpts).ConfigureAwait(true);
            AgentsCollection.ItemsSource = devices ?? [];
        }
        catch { /* ignore */ }
        try
        {
            var rawTokens = await client.GetFromJsonAsync<List<InviteTokenDto>>($"{relay}/v1/invite-tokens", JsonOpts).ConfigureAwait(true);
            var active = rawTokens?.Where(t => t.IsValid).Select(t => new InviteTokenViewModel(t)).ToList() ?? [];
            TokensCollection.ItemsSource = active;
        }
        catch { /* ignore */ }
    }

    private async void OnGenerateInviteTokenAsync(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        if (!s.IsRegistered) { ShowStatus("Register first (Setup tab).", false); return; }
        if (InviteRoomPicker.SelectedIndex < 0) { ShowStatus("Select a room.", false); return; }
        if (InviteRolePicker.SelectedIndex < 0) { ShowStatus("Select a role.", false); return; }

        var rooms = InviteRoomPicker.BindingContext as List<RoomDto>;
        if (rooms is null || InviteRoomPicker.SelectedIndex >= rooms.Count) return;
        var selectedRoom = rooms[InviteRoomPicker.SelectedIndex];

        var role = InviteRolePicker.Items[InviteRolePicker.SelectedIndex];
        if (!int.TryParse(MaxUsesEntry.Text, out var maxUses))
            maxUses = 1;

        using var client = MakeClient(s);
        var payload = new { roomId = Guid.Parse(selectedRoom.RoomId), role, maxUses };
        try
        {
            var resp = await client.PostAsJsonAsync($"{s.RelayUrl.TrimEnd('/')}/v1/invite-tokens", payload, JsonOpts).ConfigureAwait(true);
            if (!resp.IsSuccessStatusCode) { ShowStatus("Failed to create token.", false); return; }
            var token = await resp.Content.ReadFromJsonAsync<InviteTokenDto>(JsonOpts).ConfigureAwait(true);
            if (token is null) return;

            _lastTokenCommand = $"federation register --relay {s.RelayUrl} --name <AgentName> --invite {token.TokenCode}";
            TokenCommandLabel.Text = _lastTokenCommand;
            TokenResultBox.IsVisible = true;
            ShowStatus("Token created.", true);
            await RefreshAllAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
        }
    }

    private void OnCopyTokenCommand(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastTokenCommand))
        {
            Clipboard.SetTextAsync(_lastTokenCommand);
            ShowStatus("Command copied to clipboard.", true);
        }
    }

    private HttpClient MakeClient(AppSettings s)
    {
        var client = _http.CreateClient();
        if (s.DeviceId is not null)    client.DefaultRequestHeaders.Add("X-Device-Id",    s.DeviceId);
        if (s.DeviceToken is not null) client.DefaultRequestHeaders.Add("X-Device-Token", s.DeviceToken);
        return client;
    }

    private void ShowStatus(string message, bool success)
    {
        StatusLabel.Text = message;
        StatusBanner.BackgroundColor = success ? Color.FromArgb("#d1f7dd") : Color.FromArgb("#fde8e8");
        StatusLabel.TextColor = success ? Color.FromArgb("#1a6631") : Color.FromArgb("#9b1111");
        StatusBanner.IsVisible = true;
    }

    // DTOs
    private sealed record RoomDto(string RoomId, string Name, int MemberCount);
    private sealed record DeviceDto(string DeviceId, string DisplayName, DateTimeOffset RegisteredAt);
    private sealed record InviteTokenDto(string TokenCode, string RoomId, string Role, int MaxUses, int UsedCount, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool IsRevoked, bool IsValid);

    private sealed class InviteTokenViewModel(InviteTokenDto t)
    {
        public string TokenCode    { get; } = t.TokenCode;
        public string UsageSummary { get; } = $"{t.Role} · {(t.MaxUses == 0 ? $"{t.UsedCount}/∞ uses" : $"{t.UsedCount}/{t.MaxUses} uses")} · expires {(t.ExpiresAt.HasValue ? t.ExpiresAt.Value.ToString("MMM d", System.Globalization.CultureInfo.CurrentCulture) : "never")}";
    }
}
