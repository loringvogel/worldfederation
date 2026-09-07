using Federation.Protocol;

namespace Federation.Identity;

/// <summary>Represents a device's identity in the federation.</summary>
public sealed record DeviceIdentity
{
    public required DeviceId Id { get; init; }
    public required string DisplayName { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto key material.")]
    public required byte[] PublicSigningKey { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto key material.")]
    public required byte[] PublicKeyAgreementKey { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
