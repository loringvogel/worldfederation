using System.Threading.Channels;
using Federation.Protocol;

namespace Federation.Relay.Stores;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly Channel<DomainEvent> _channel = Channel.CreateUnbounded<DomainEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(domainEvent, ct).ConfigureAwait(false);
    }

    public IAsyncEnumerable<DomainEvent> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
