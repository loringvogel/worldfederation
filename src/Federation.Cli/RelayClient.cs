using System.Net.Http.Json;

namespace Federation.Cli;

/// <summary>
/// Typed HTTP client for relay management operations.
/// Auth headers (X-Device-Id, X-Device-Token) are set from NodeConfig when available.
/// </summary>
public sealed class RelayClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _relayUrl;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public RelayClient(string relayUrl, string? deviceId = null, string? deviceToken = null)
    {
        _relayUrl = relayUrl.TrimEnd('/');
        _http = new HttpClient();
        if (deviceId is not null) _http.DefaultRequestHeaders.Add("X-Device-Id", deviceId);
        if (deviceToken is not null) _http.DefaultRequestHeaders.Add("X-Device-Token", deviceToken);
    }

    /// <summary>POST /v1/devices/registrations</summary>
    public async Task<DeviceRegistrationResponse?> RegisterDeviceAsync(string displayName)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_relayUrl}/v1/devices/registrations",
                new { DisplayName = displayName }, JsonOpts).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "register device").ConfigureAwait(false);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(JsonOpts).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error registering device: {ex.Message}");
            return null;
        }
    }

    /// <summary>POST /v1/rooms</summary>
    public async Task<RoomSummaryResponse?> CreateRoomAsync(string name, string ownerDeviceId)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_relayUrl}/v1/rooms",
                new { Name = name, OwnerDeviceId = ownerDeviceId }, JsonOpts).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "create room").ConfigureAwait(false);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOpts).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error creating room: {ex.Message}");
            return null;
        }
    }

    /// <summary>POST /v1/rooms/{roomId}/invitations</summary>
    public async Task<MembershipResponse?> InviteDeviceAsync(string roomId, string deviceId, string role = "Participant")
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_relayUrl}/v1/rooms/{roomId}/invitations",
                new { DeviceId = deviceId, Role = role }, JsonOpts).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "invite device").ConfigureAwait(false);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<MembershipResponse>(JsonOpts).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error inviting device: {ex.Message}");
            return null;
        }
    }

    /// <summary>GET /v1/rooms</summary>
    public async Task<List<RoomSummaryResponse>> ListRoomsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_relayUrl}/v1/rooms").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "list rooms").ConfigureAwait(false);
                return [];
            }
            return await response.Content.ReadFromJsonAsync<List<RoomSummaryResponse>>(JsonOpts).ConfigureAwait(false) ?? [];
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error listing rooms: {ex.Message}");
            return [];
        }
    }

    /// <summary>POST /v1/rooms/{roomId}/discussions</summary>
    public async Task<DiscussionStateResponse?> CreateDiscussionAsync(string roomId, string topic, string initiatorDeviceId)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_relayUrl}/v1/rooms/{roomId}/discussions",
                new { Topic = topic, InitiatorDeviceId = initiatorDeviceId }, JsonOpts).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "create discussion").ConfigureAwait(false);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<DiscussionStateResponse>(JsonOpts).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error creating discussion: {ex.Message}");
            return null;
        }
    }

    /// <summary>GET /v1/rooms/{roomId}/discussions</summary>
    public async Task<List<DiscussionStateResponse>> ListDiscussionsAsync(string roomId)
    {
        try
        {
            var response = await _http.GetAsync($"{_relayUrl}/v1/rooms/{roomId}/discussions").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "list discussions").ConfigureAwait(false);
                return [];
            }
            return await response.Content.ReadFromJsonAsync<List<DiscussionStateResponse>>(JsonOpts).ConfigureAwait(false) ?? [];
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error listing discussions: {ex.Message}");
            return [];
        }
    }

    /// <summary>GET /v1/rooms/{roomId}/discussions/{discussionId}/state</summary>
    public async Task<DiscussionStateResponse?> GetDiscussionStateAsync(string roomId, string discussionId)
    {
        try
        {
            var response = await _http.GetAsync($"{_relayUrl}/v1/rooms/{roomId}/discussions/{discussionId}/state").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "get discussion state").ConfigureAwait(false);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<DiscussionStateResponse>(JsonOpts).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error getting discussion state: {ex.Message}");
            return null;
        }
    }

    /// <summary>GET /v1/rooms/{roomId}/members</summary>
    public async Task<List<RosterEntryResponse>> GetMembersAsync(string roomId)
    {
        try
        {
            var response = await _http.GetAsync($"{_relayUrl}/v1/rooms/{roomId}/members").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "get members").ConfigureAwait(false);
                return [];
            }
            return await response.Content.ReadFromJsonAsync<List<RosterEntryResponse>>(JsonOpts).ConfigureAwait(false) ?? [];
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error getting members: {ex.Message}");
            return [];
        }
    }

    /// <summary>GET /health</summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_relayUrl}/health").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>POST /v1/devices/{deviceId}/emergency-revoke</summary>
    public async Task EmergencyRevokeAsync(string deviceId)
    {
        try
        {
            var response = await _http.PostAsync($"{_relayUrl}/v1/devices/{deviceId}/emergency-revoke", null).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(response, "emergency revoke").ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error during emergency revoke: {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private static async Task WriteErrorAsync(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Console.Error.WriteLine($"Failed to {operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
    }
}

/// <summary>Device registration response from the relay.</summary>
public sealed record DeviceRegistrationResponse(string DeviceId, string DisplayName, string DeviceToken, DateTimeOffset RegisteredAt);

/// <summary>Room summary response from the relay.</summary>
public sealed record RoomSummaryResponse(string RoomId, string Name, DateTimeOffset CreatedAt, int MemberCount);

/// <summary>Membership response from the relay.</summary>
public sealed record MembershipResponse(string MembershipId, string RoomId, string DeviceId, string Role, DateTimeOffset JoinedAt);

/// <summary>Discussion state response from the relay.</summary>
public sealed record DiscussionStateResponse(string DiscussionId, string RoomId, string Topic, string Phase, int CurrentRound, DateTimeOffset CreatedAt, int TotalSubmissions, int ExpectedSubmissions);

/// <summary>Room member roster entry from the relay.</summary>
public sealed record RosterEntryResponse(string DeviceId, string DisplayName, string Role, DateTimeOffset JoinedAt);
