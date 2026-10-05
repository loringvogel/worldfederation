using Federation.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace Federation.Relay.Routes;

public static class InviteTokenRoutes
{
    public static RouteGroupBuilder MapInviteTokenRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/invite-tokens");

        // POST /v1/invite-tokens — create a new invite token (requires auth)
        group.MapPost("/", async (
            [FromBody] CreateInviteTokenRequest request,
            IInviteTokenRepository tokens,
            IRoomRepository rooms,
            HttpContext ctx) =>
        {
            // Verify the room exists
            var roomId = new RoomId(request.RoomId);
            var room = await rooms.GetRoomAsync(roomId).ConfigureAwait(false);
            if (room is null) return Results.NotFound("Room not found.");

            var expiresAt = request.ExpiresInHours.HasValue
                ? DateTimeOffset.UtcNow.AddHours(request.ExpiresInHours.Value)
                : (DateTimeOffset?)null;

            var token = await tokens.CreateAsync(
                roomId,
                request.Role,
                request.MaxUses,
                expiresAt).ConfigureAwait(false);

            return Results.Created($"/v1/invite-tokens/{token.TokenCode}",
                new InviteTokenResponse(token));
        });

        // GET /v1/invite-tokens — list all tokens
        group.MapGet("/", async (IInviteTokenRepository tokens) =>
        {
            var list = await tokens.ListAsync().ConfigureAwait(false);
            return Results.Ok(list.Select(t => new InviteTokenResponse(t)));
        });

        // DELETE /v1/invite-tokens/{code} — revoke a token
        group.MapDelete("/{code}", async (
            string code,
            IInviteTokenRepository tokens) =>
        {
            var ok = await tokens.RevokeAsync(code).ConfigureAwait(false);
            return ok ? Results.NoContent() : Results.NotFound("Token not found.");
        });

        return group;
    }
}

public sealed record CreateInviteTokenRequest
{
    public required Guid RoomId         { get; init; }
    public required MemberRole Role     { get; init; }
    public int MaxUses                  { get; init; } = 1;
    public double? ExpiresInHours       { get; init; }
}

public sealed record InviteTokenResponse(
    string TokenCode,
    string RoomId,
    string Role,
    int MaxUses,
    int UsedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked,
    bool IsValid)
{
    public InviteTokenResponse(InviteToken t) : this(
        (t ?? throw new ArgumentNullException(nameof(t))).TokenCode,
        t.RoomId.Value.ToString(),
        t.Role.ToString(),
        t.MaxUses,
        t.UsedCount,
        t.CreatedAt,
        t.ExpiresAt,
        t.IsRevoked,
        t.IsValid) { }
}
