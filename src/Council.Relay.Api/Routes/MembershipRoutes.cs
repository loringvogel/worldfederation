using Council.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Council.Relay.Api.Routes;

public static class MembershipRoutes
{
    public static RouteGroupBuilder MapMembershipRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/rooms/{roomId}");

        group.MapPost("/invitations", async (
            Guid roomId,
            [FromBody] InvitationRequest request,
            IMembershipRepository memberships,
            IRoomRepository rooms) =>
        {
            var rid = new RoomId(roomId);
            var room = await rooms.GetRoomAsync(rid).ConfigureAwait(false);
            if (room is null)
            {
                return Results.NotFound("Room not found.");
            }

            var membership = await memberships.AddMembershipAsync(rid, request.DeviceId, request.Role).ConfigureAwait(false);
            return Results.Created($"/v1/rooms/{roomId}/memberships/{membership.MembershipId}", membership);
        });

        group.MapPost("/memberships/{membershipId}/revoke", async (
            Guid roomId,
            Guid membershipId,
            IMembershipRepository memberships,
            IEventBus eventBus) =>
        {
            var mid = new MembershipId(membershipId);
            var existing = await memberships.GetMembershipAsync(mid).ConfigureAwait(false);
            if (existing is null)
            {
                return Results.NotFound("Membership not found.");
            }

            await memberships.RevokeMembershipAsync(mid).ConfigureAwait(false);

            await eventBus.PublishAsync(new MemberRevoked
            {
                RoomId = new RoomId(roomId),
                MembershipId = mid,
                DeviceId = existing.DeviceId,
            }).ConfigureAwait(false);

            return Results.NoContent();
        });

        return group;
    }
}

public sealed record InvitationRequest
{
    public required DeviceId DeviceId { get; init; }
    public required MemberRole Role { get; init; }
}
