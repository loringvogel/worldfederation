using Council.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Council.Relay.Api.Routes;

public static class RoomRoutes
{
    public static RouteGroupBuilder MapRoomRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/rooms");

        group.MapGet("/", async (IRoomRepository rooms) =>
        {
            var list = await rooms.ListRoomsAsync().ConfigureAwait(false);
            return Results.Ok(list);
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
