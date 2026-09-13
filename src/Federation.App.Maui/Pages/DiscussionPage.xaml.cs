using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace Federation.App.Maui.Pages;

public partial class DiscussionPage : ContentPage
{
    private readonly AppSettingsService _settings;
    private readonly IHttpClientFactory _http;

    private readonly ObservableCollection<MessageViewModel> _messages = [];
    private List<RoomDto> _rooms = [];
    private List<DiscussionDto> _discussions = [];
    private DiscussionDto? _activeDisc;
    private IDispatcherTimer? _pollTimer;

    public ObservableCollection<MessageViewModel> Messages => _messages;

    public DiscussionPage(AppSettingsService settings, IHttpClientFactory http)
    {
        InitializeComponent();
        _settings = settings;
        _http = http;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Ensure DeviceToken is loaded from DPAPI/Keychain before any relay call.
        await _settings.InitializeAsync().ConfigureAwait(true);

        if (!_settings.Current.IsRegistered)
        {
            PhaseLabel.Text = "Not registered — go to Setup tab.";
            return;
        }
        await LoadAsync();
        _pollTimer = Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(10);
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _pollTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollTimer?.Stop();
    }

    // ── Data loading ────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        try
        {
            using var c = NewClient();
            var rooms = await c.GetFromJsonAsync<List<RoomDto>>("v1/rooms") ?? [];
            _rooms = rooms;
            RoomPicker.ItemsSource = _rooms.Select(r => $"{r.Name} ({r.MemberCount})").ToList();

            var activeRoomIdx = _rooms.FindIndex(r => r.RoomId == _settings.Current.ActiveRoomId);
            if (activeRoomIdx >= 0) RoomPicker.SelectedIndex = activeRoomIdx;

            await LoadDiscussionsAsync();
        }
        catch (Exception ex)
        {
            PhaseLabel.Text = $"Cannot reach relay: {ex.Message}";
        }
    }

    private async Task LoadDiscussionsAsync()
    {
        var roomId = SelectedRoomId();
        if (roomId is null) return;
        try
        {
            using var c = NewClient();
            _discussions = await c.GetFromJsonAsync<List<DiscussionDto>>($"v1/rooms/{roomId}/discussions") ?? [];
            DiscPicker.ItemsSource = _discussions
                .Select(d => $"{Truncate(d.Topic, 30)} — {d.Phase}")
                .ToList();

            var activeIdx = _discussions.FindIndex(d => d.DiscussionId == _settings.Current.ActiveDiscussionId);
            if (activeIdx >= 0)
                DiscPicker.SelectedIndex = activeIdx;
            else if (_discussions.Count > 0)
                DiscPicker.SelectedIndex = 0;

            UpdateActiveDiscussion();
            await LoadMessagesAsync();
        }
        catch { /* relay may not be running */ }
    }

    private void UpdateActiveDiscussion()
    {
        var idx = DiscPicker.SelectedIndex;
        _activeDisc = idx >= 0 && idx < _discussions.Count ? _discussions[idx] : null;
        if (_activeDisc is not null)
        {
            PhaseLabel.Text = _activeDisc.Phase;
            RoundLabel.Text = $"Round {_activeDisc.CurrentRound}";
            SubmissionsLabel.Text = $"{_activeDisc.TotalSubmissions}/{_activeDisc.ExpectedSubmissions} submitted";
        }
        else
        {
            PhaseLabel.Text = "Select a discussion";
            RoundLabel.Text = string.Empty;
            SubmissionsLabel.Text = string.Empty;
        }
        BuildActionButtons();
    }

    private async Task LoadMessagesAsync()
    {
        var roomId = SelectedRoomId();
        var discId = SelectedDiscussionId();
        if (roomId is null || discId is null) return;
        try
        {
            using var c = NewClient();
            var resp = await c.GetFromJsonAsync<EnvelopesResponse>(
                $"v1/rooms/{roomId}/discussions/{discId}/envelopes?after=0");
            if (resp is null) return;
            _messages.Clear();
            foreach (var e in resp.Envelopes)
                _messages.Add(new MessageViewModel(e));
        }
        catch { }
    }

    private async Task PollAsync()
    {
        await LoadDiscussionsAsync();
    }

    private async void OnRefreshAsync(object? sender, EventArgs e) => await LoadAsync();

    private async void OnRoomChanged(object? sender, EventArgs e)
    {
        var idx = RoomPicker.SelectedIndex;
        if (idx < 0 || idx >= _rooms.Count) return;
        var s = _settings.Current;
        s.ActiveRoomId = _rooms[idx].RoomId;
        s.ActiveDiscussionId = null;
        _settings.Apply(s);
        _messages.Clear();
        _discussions.Clear();
        DiscPicker.ItemsSource = null;
        _activeDisc = null;
        UpdateActiveDiscussion();
        await LoadDiscussionsAsync();
    }

    private async void OnDiscussionChanged(object? sender, EventArgs e)
    {
        var idx = DiscPicker.SelectedIndex;
        if (idx < 0 || idx >= _discussions.Count) return;
        var s = _settings.Current;
        s.ActiveDiscussionId = _discussions[idx].DiscussionId;
        _settings.Apply(s);
        UpdateActiveDiscussion();
        await LoadMessagesAsync();
    }

    // ── Submit buttons ───────────────────────────────────────────────────

    private void BuildActionButtons()
    {
        ActionButtons.Children.Clear();
        if (_activeDisc is null) return;

        var buttons = _activeDisc.Phase switch
        {
            "ProposalRound" => new[] { ("Propose",   "#0a84ff", "Proposal") },
            "CritiqueRound" => new[] { ("Critique",  "#ff9f0a", "Critique") },
            "RevisionRound" => new[] { ("Revise",    "#0a84ff", "Revision") },
            "Vote"          => new[]
            {
                ("Approve",  "#30d158", "Vote:approve"),
                ("Reject",   "#ff3b30", "Vote:reject"),
                ("Abstain",  "#8e8e93", "Vote:abstain"),
            },
            "Synthesis"     => new[] { ("Synthesize", "#ff375f", "Synthesis") },
            _               => Array.Empty<(string, string, string)>(),
        };

        foreach (var (label, color, type) in buttons)
        {
            var btn = new Button
            {
                Text            = label,
                BackgroundColor = Color.FromArgb(color),
                TextColor       = Colors.White,
                CornerRadius    = 8,
                FontSize        = 13,
                HeightRequest   = 36,
                CommandParameter = type,
            };
            btn.Clicked += OnSubmitClicked;
            ActionButtons.Children.Add(btn);
        }
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var type = btn.CommandParameter as string ?? string.Empty;
        var text = ComposeEditor.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var roomId = SelectedRoomId();
        var discId = SelectedDiscussionId();
        if (roomId is null || discId is null) return;

        btn.IsEnabled = false;
        try
        {
            var s = _settings.Current;
            using var c = _http.CreateClient();
            var payload = new { RoomId = roomId, DiscussionId = discId, MessageType = type, Text = text };
            var resp = await c.PostAsJsonAsync(
                new Uri(s.RelayUrl.TrimEnd('/') + "/v1/agent/submit"), payload);

            if (resp.IsSuccessStatusCode)
            {
                ComposeEditor.Text = string.Empty;
                await LoadMessagesAsync();
            }
            else
            {
                await DisplayAlert("Submit Failed", $"HTTP {(int)resp.StatusCode}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private HttpClient NewClient()
    {
        var s = _settings.Current;
        var c = _http.CreateClient();
        c.BaseAddress = new Uri(s.RelayUrl.TrimEnd('/') + "/");
        c.DefaultRequestHeaders.Add("X-Device-Id", s.DeviceId);
        c.DefaultRequestHeaders.Add("X-Device-Token", s.DeviceToken);
        return c;
    }

    private string? SelectedRoomId()
    {
        var idx = RoomPicker.SelectedIndex;
        return idx >= 0 && idx < _rooms.Count ? _rooms[idx].RoomId : null;
    }

    private string? SelectedDiscussionId()
    {
        var idx = DiscPicker.SelectedIndex;
        return idx >= 0 && idx < _discussions.Count ? _discussions[idx].DiscussionId : null;
    }

    private static string Truncate(string? s, int max) =>
        s is null ? string.Empty : s.Length <= max ? s : s[..max];

    // ── DTOs ─────────────────────────────────────────────────────────────
    private sealed class RoomDto
    {
        public string RoomId      { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;
        public int    MemberCount { get; set; }
    }

    private sealed class DiscussionDto
    {
        public string         DiscussionId        { get; set; } = string.Empty;
        public string         Topic               { get; set; } = string.Empty;
        public string         Phase               { get; set; } = string.Empty;
        public int            CurrentRound        { get; set; }
        public int            TotalSubmissions    { get; set; }
        public int            ExpectedSubmissions { get; set; }
    }

    private sealed class EnvelopesResponse
    {
        public List<EnvelopeDto> Envelopes { get; set; } = [];
    }

    private sealed class EnvelopeDto
    {
        public string         MessageType    { get; set; } = string.Empty;
        public int            Round          { get; set; }
        public string         SenderDeviceId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt      { get; set; }
    }
}

/// <summary>View model for a single message row in the feed. All properties are set once in the constructor.</summary>
public sealed class MessageViewModel
{
    private static readonly Dictionary<string, string> TypeColors = new()
    {
        ["Proposal"]  = "#0a84ff",
        ["Critique"]  = "#ff9f0a",
        ["Revision"]  = "#30d158",
        ["Vote"]      = "#bf5af2",
        ["Synthesis"] = "#ff375f",
    };

    public string TypeLabel   { get; }
    public string TypeColor   { get; }
    public string RoundLabel  { get; }
    public string TimeLabel   { get; }
    public string ShortSender { get; }
    public string Plaintext   { get; }

    public MessageViewModel(object envelope)
    {
        // Deserialize via JsonElement to stay loosely coupled
        var json = JsonSerializer.Serialize(envelope);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var type = root.TryGetProperty("messageType", out var mt) ? mt.GetString() ?? "?" : "?";
        TypeLabel   = type;
        TypeColor   = TypeColors.GetValueOrDefault(type, "#8e8e93");
        RoundLabel  = root.TryGetProperty("round", out var r) ? $"Round {r.GetInt32()}" : string.Empty;
        Plaintext   = "[encrypted — poll via CLI to decrypt]";
        ShortSender = root.TryGetProperty("senderDeviceId", out var sid)
            ? (sid.GetString() ?? string.Empty)[..Math.Min(8, sid.GetString()?.Length ?? 0)] + "…"
            : string.Empty;
        TimeLabel   = root.TryGetProperty("createdAt", out var ts) && ts.TryGetDateTimeOffset(out var dt)
            ? dt.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
