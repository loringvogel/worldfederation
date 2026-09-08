using Federation.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace Federation.Relay.Routes;

public static class DiscussionRoutes
{
    public static RouteGroupBuilder MapDiscussionRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/rooms/{roomId}/discussions");

        group.MapPost("/", async (
            Guid roomId,
            [FromBody] CreateDiscussionApiRequest request,
            IDiscussionRepository discussions,
            IRoomRepository rooms) =>
        {
            var rid = new RoomId(roomId);
            var room = await rooms.GetRoomAsync(rid).ConfigureAwait(false);
            if (room is null)
            {
                return Results.NotFound("Room not found.");
            }

            var createRequest = new CreateDiscussionRequest
            {
                RoomId = rid,
                Topic = request.Topic,
                InitiatorDeviceId = request.InitiatorDeviceId,
            };

            var state = await discussions.CreateDiscussionAsync(createRequest).ConfigureAwait(false);
            return Results.Created($"/v1/rooms/{roomId}/discussions/{state.DiscussionId}", state);
        });

        group.MapGet("/", async (
            Guid roomId,
            IDiscussionRepository discussions,
            IMembershipRepository memberships,
            HttpContext httpContext) =>
        {
            var rid = new RoomId(roomId);

            // Cell isolation: verify the requesting device is a member of this room.
            // Return 404 (not 403) to avoid leaking whether the room exists.
            if (!httpContext.Request.Headers.TryGetValue("X-Device-Id", out var deviceIdHeader) ||
                !Guid.TryParse(deviceIdHeader.ToString(), out var parsedDeviceId))
            {
                return Results.NotFound();
            }

            var deviceId = new DeviceId(parsedDeviceId);
            var membership = await memberships.GetMembershipByDeviceAsync(rid, deviceId).ConfigureAwait(false);
            if (membership is null)
            {
                return Results.NotFound();
            }

            var result = await discussions.GetDiscussionsInRoomAsync(rid).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapGet("/{discussionId}/state", async (
            Guid roomId,
            Guid discussionId,
            IDiscussionRepository discussions) =>
        {
            var state = await discussions.GetDiscussionAsync(new DiscussionId(discussionId)).ConfigureAwait(false);
            if (state is null)
            {
                return Results.NotFound("Discussion not found.");
            }

            return Results.Ok(state);
        });

        return group;
    }
}

public sealed record CreateDiscussionApiRequest
{
    public required string Topic { get; init; }
    public required DeviceId InitiatorDeviceId { get; init; }
}
