using Federation.Identity;

namespace Federation.Discovery;

/// <summary>Discovers peers on the network.</summary>
public interface IPeerDiscovery
{
    IAsyncEnumerable<PeerRecord> DiscoverAsync(CancellationToken ct);
}
