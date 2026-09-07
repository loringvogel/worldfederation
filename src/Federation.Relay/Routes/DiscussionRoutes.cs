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
