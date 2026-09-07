using System.Threading.Channels;
using Council.Contracts;

namespace Council.Relay.Api.Stores;

/// <summary>
/// In-memory event bus for Phase 1 simulation using System.Threading.Channels.
/// Events are published to an unbounded channel for in-process consumers.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly Channel<DomainEvent> _channel = Channel.CreateUnbounded<DomainEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(domainEvent, ct).ConfigureAwait(false);
    }

    /// <summary>Reads events from the channel. Used by background consumers.</summary>
    public IAsyncEnumerable<DomainEvent> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
