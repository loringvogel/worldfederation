using Federation.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace Federation.Relay.Routes;

public static class RoomRoutes
{
    public static RouteGroupBuilder MapRoomRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/rooms");

        group.MapGet("/", async (
            IRoomRepository rooms,
            IMembershipRepository memberships,
            HttpContext httpContext) =>
        {
            // Cell isolation: only return rooms where the requesting device has an active membership.
            // A compromised relay cannot enumerate rooms for devices it doesn't control.
            if (!httpContext.Request.Headers.TryGetValue("X-Device-Id", out var deviceIdHeader) ||
                !Guid.TryParse(deviceIdHeader.ToString(), out var parsedDeviceId))
            {
                return Results.Unauthorized();
            }

            var deviceId = new DeviceId(parsedDeviceId);
            var memberRoomIds = await memberships.GetRoomsForDeviceAsync(deviceId).ConfigureAwait(false);

            var result = new List<RoomSummary>();
            foreach (var roomId in memberRoomIds)
            {
                var room = await rooms.GetRoomAsync(roomId).ConfigureAwait(false);
                if (room is not null)
                {
                    result.Add(room);
                }
            }

            return Results.Ok(result);
        });

        group.MapPost("/", async (
            [FromBody] CreateRoomRequest request,
            IRoomRepository rooms) =>
        {
            var room = await rooms.CreateRoomAsync(request).ConfigureAwait(false);
            return Results.Created($"/v1/rooms/{room.RoomId}", room);
        });

        group.MapGet("/public", async (IRoomRepository rooms) =>
        {
            var list = await rooms.ListPublicRoomsAsync().ConfigureAwait(false);
            return Results.Ok(list);
        });

        group.MapPost("/{roomId}/join", async (
            Guid roomId,
            IRoomRepository rooms,
            IMembershipRepository memberships,
            IDeviceRepository devices,
            HttpContext ctx) =>
        {
            if (!ctx.Request.Headers.TryGetValue("X-Device-Id", out var devHeader) ||
                !Guid.TryParse(devHeader.ToString(), out var parsedDeviceId))
                return Results.Unauthorized();

            var rid = new RoomId(roomId);
            var room = await rooms.GetRoomAsync(rid).ConfigureAwait(false);
            if (room is null) return Results.NotFound("Room not found.");
            if (!room.IsPublic) return Results.Forbid();

            var deviceId = new DeviceId(parsedDeviceId);
            // Idempotent: skip if already a member
            var existing = await memberships.GetMembershipByDeviceAsync(rid, deviceId).ConfigureAwait(false);
            if (existing is not null) return Results.Ok(existing);

            var membership = await memberships.AddMembershipAsync(rid, deviceId, MemberRole.Participant).ConfigureAwait(false);
            return Results.Created($"/v1/rooms/{roomId}/members/{membership.MembershipId}", membership);
        });

        return group;
    }
}
