using Federation.Protocol;

namespace Federation.Transport;

/// <summary>Transport layer for relay-mediated communication.</summary>
public interface IRelayTransport
{
    Task SubmitEnvelopeAsync(string relayUrl, MessageEnvelope envelope, CancellationToken ct = default);
    Task<IReadOnlyList<MessageEnvelope>> PollEnvelopesAsync(string relayUrl, RoomId roomId, DiscussionId discussionId, string? afterCursor, CancellationToken ct = default);
}
