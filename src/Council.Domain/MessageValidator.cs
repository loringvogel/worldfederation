using Council.Contracts;

namespace Council.Domain;

/// <summary>
/// Validates envelope fields before the relay accepts a message.
/// These are relay-side checks (spec section 12).
/// </summary>
public static class MessageValidator
{
    /// <summary>
    /// Validates an envelope against policy and current state.
    /// </summary>
    /// <returns>A list of validation errors; empty if the envelope is valid.</returns>
    public static IReadOnlyList<string> Validate(
        MessageEnvelope envelope,
        RoomPolicy policy,
        EpochId currentEpoch,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(policy);

        var errors = new List<string>();

        // Protocol version check
        if (string.IsNullOrWhiteSpace(envelope.ProtocolVersion))
        {
            errors.Add("ProtocolVersion is required.");
        }

        // Epoch must match
        if (envelope.Epoch != currentEpoch)
        {
            errors.Add($"Epoch mismatch: envelope has {envelope.Epoch.Value}, expected {currentEpoch.Value}.");
        }

        // Sequence must be positive
        if (envelope.SenderSequence < 0)
        {
            errors.Add("SenderSequence must be non-negative.");
        }

        // Expiry must not be in the past
        if (envelope.ExpiresAt <= now)
        {
            errors.Add("Envelope has expired (ExpiresAt is in the past).");
        }

        // CreatedAt should not be in the future (with a small tolerance)
        if (envelope.CreatedAt > now.AddMinutes(5))
        {
            errors.Add("Envelope CreatedAt is too far in the future.");
        }

        // Ciphertext size must be within policy limits
        if (envelope.Ciphertext.Length > policy.MaxMessageSizeBytes)
        {
            errors.Add($"Ciphertext size {envelope.Ciphertext.Length} exceeds maximum {policy.MaxMessageSizeBytes} bytes.");
        }

        // Ciphertext must not be empty
        if (envelope.Ciphertext.Length == 0)
        {
            errors.Add("Ciphertext must not be empty.");
        }

        // Signature must not be empty
        if (envelope.Signature.Length == 0)
        {
            errors.Add("Signature must not be empty.");
        }

        // CipherSuite must be specified
        if (string.IsNullOrWhiteSpace(envelope.CipherSuite))
        {
            errors.Add("CipherSuite is required.");
        }

        // Round must be non-negative
        if (envelope.Round < 0)
        {
            errors.Add("Round must be non-negative.");
        }

        return errors;
    }
}
