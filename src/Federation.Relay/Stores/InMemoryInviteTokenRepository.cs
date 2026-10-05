using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Relay.Stores;

public sealed class InMemoryInviteTokenRepository : IInviteTokenRepository
{
    private readonly ConcurrentDictionary<string, InviteToken> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public Task<InviteToken> CreateAsync(RoomId roomId, MemberRole role, int maxUses = 1, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        var code = GenerateCode();
        var token = new InviteToken
        {
            TokenCode  = code,
            RoomId     = roomId,
            Role       = role,
            MaxUses    = maxUses,
            UsedCount  = 0,
            CreatedAt  = DateTimeOffset.UtcNow,
            ExpiresAt  = expiresAt,
            IsRevoked  = false,
        };
        _tokens[code] = token;
        return Task.FromResult(token);
    }

    public Task<InviteToken?> ValidateAndConsumeAsync(string tokenCode, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(tokenCode, out var token) || !token.IsValid)
            return Task.FromResult<InviteToken?>(null);
        token.UsedCount++;
        return Task.FromResult<InviteToken?>(token);
    }

    public Task<IReadOnlyList<InviteToken>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InviteToken>>(_tokens.Values.ToList());

    public Task<bool> RevokeAsync(string tokenCode, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(tokenCode, out var token)) return Task.FromResult(false);
        token.IsRevoked = true;
        return Task.FromResult(true);
    }

    private static string GenerateCode()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }
}
