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

        return group;
    }
}
