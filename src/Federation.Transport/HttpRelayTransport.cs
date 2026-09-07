using System.Net.Http.Json;
using System.Text.Json;
using Federation.Protocol;

namespace Federation.Transport;

/// <summary>Relay transport using HTTP to post and poll envelopes.</summary>
public sealed class HttpRelayTransport : IRelayTransport
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public HttpRelayTransport(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task SubmitEnvelopeAsync(string relayUrl, MessageEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var url = $"{relayUrl}/v1/rooms/{envelope.RoomId.Value}/discussions/{envelope.DiscussionId.Value}/envelopes";
        var request = new SubmitEnvelopeRequest { Envelope = envelope };
        var response = await _httpClient.PostAsJsonAsync(new Uri(url), request, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MessageEnvelope>> PollEnvelopesAsync(string relayUrl, RoomId roomId, DiscussionId discussionId, string? afterCursor, CancellationToken ct = default)
    {
        var cursor = afterCursor ?? "0";
        var url = $"{relayUrl}/v1/rooms/{roomId.Value}/discussions/{discussionId.Value}/envelopes?after={cursor}";
        var response = await _httpClient.GetFromJsonAsync<GetEnvelopesResponse>(new Uri(url), JsonOptions, ct).ConfigureAwait(false);
        return response?.Envelopes ?? [];
    }
}
