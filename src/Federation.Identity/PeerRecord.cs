using Federation.Protocol;

namespace Federation.Identity;

/// <summary>A signed record describing a peer device in the federation.</summary>
public sealed record PeerRecord
{
    public required DeviceId DeviceId { get; init; }
    public required HumanId HumanId { get; init; }

    /// <summary>Addresses where this peer can be reached (e.g. "tcp://192.168.1.5:7777", "relay://relay.example.com").</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration record; immutable after init.")]
    public required string[] Addresses { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration record; immutable after init.")]
    public required string[] SupportedProtocolVersions { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration record; immutable after init.")]
    public required string[] Capabilities { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto signature material.")]
    public required byte[] Signature { get; init; }
}
