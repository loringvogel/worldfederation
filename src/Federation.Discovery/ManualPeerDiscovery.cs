using System.Runtime.CompilerServices;
using Federation.Identity;
using Federation.Protocol;

namespace Federation.Discovery;

/// <summary>
/// Accepts a list of pre-configured addresses. Useful for bootstrap nodes and private deployments.
/// </summary>
public sealed class ManualPeerDiscovery : IPeerDiscovery
{
    private readonly IReadOnlyList<PeerRecord> _peers;

    public ManualPeerDiscovery(IReadOnlyList<PeerRecord> peers)
    {
        ArgumentNullException.ThrowIfNull(peers);
        _peers = peers;
    }

    public async IAsyncEnumerable<PeerRecord> DiscoverAsync([EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var peer in _peers)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return peer;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
