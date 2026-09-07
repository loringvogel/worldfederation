using Federation.Protocol;

namespace Federation.AgentGateway;

/// <summary>A decrypted message as seen by the agent gateway.</summary>
public sealed record DecryptedMessage
{
    public required MessageId Id { get; init; }
    public required DeviceId SenderDeviceId { get; init; }
    public required MessageType Type { get; init; }
    public required int Round { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
