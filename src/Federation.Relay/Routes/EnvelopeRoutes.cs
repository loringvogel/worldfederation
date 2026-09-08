using Federation.Protocol;
using Federation.Deliberation;
using Microsoft.AspNetCore.Mvc;

namespace Federation.Relay.Routes;

public static class EnvelopeRoutes
{
    public static RouteGroupBuilder MapEnvelopeRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/rooms/{roomId}/discussions/{discussionId}");

        group.MapPost("/envelopes", async (
            Guid roomId,
            Guid discussionId,
            [FromBody] SubmitEnvelopeRequest request,
            IMessageStore messages,
            IDiscussionRepository discussions,
            IMembershipRepository memberships,
            ISecurityEventStore securityEvents,
            IEventBus eventBus,
            HttpContext httpContext) =>
        {
            var rid = new RoomId(roomId);
            var did = new DiscussionId(discussionId);
            var envelope = request.Envelope;

            // Phase 1 auth: validate X-Device-Id header matches sender
            if (!TryAuthenticateDevice(httpContext, envelope.SenderDeviceId))
            {
                return Results.Unauthorized();
            }

            // Cell isolation: verify device membership is scoped to this specific room.
            // The relay must not reveal whether a device exists in other rooms.
            // A 404 and a 403 response must be indistinguishable to prevent room enumeration.
            var membership = await memberships.GetMembershipByDeviceAsync(rid, envelope.SenderDeviceId).ConfigureAwait(false);
            if (membership is null)
            {
                return Results.NotFound();
            }

            // Check discussion exists and is in a valid phase
            var discussion = await discussions.GetDiscussionAsync(did).ConfigureAwait(false);
            if (discussion is null)
            {
                return Results.NotFound("Discussion not found.");
            }

            // Room ID must match
            if (envelope.RoomId != rid)
            {
                return Results.BadRequest("Room ID in envelope does not match route.");
            }

            // Discussion ID must match
            if (envelope.DiscussionId != did)
            {
                return Results.BadRequest("Discussion ID in envelope does not match route.");
            }

            // Validate envelope fields (relay-side checks per spec section 12)
            var policy = new RoomPolicy();
            var now = DateTimeOffset.UtcNow;
            // Phase 1: use epoch 1 as the current epoch since we don't track epochs on the relay
            var validationErrors = MessageValidator.Validate(envelope, policy, envelope.Epoch, now);
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { Errors = validationErrors });
            }

            // Check for duplicate message ID
            if (await messages.ExistsAsync(envelope.MessageId).ConfigureAwait(false))
            {
                await securityEvents.AppendAsync(new SecurityEvent
                {
                    OccurredAt = now,
                    EventType = "DuplicateMessageId",
                    Description = $"Duplicate message ID {envelope.MessageId} from device {envelope.SenderDeviceId}.",
                    DeviceId = envelope.SenderDeviceId,
                    RoomId = rid,
                }).ConfigureAwait(false);
                return Results.Conflict("Duplicate message ID.");
            }

            // Store the encrypted envelope (relay never decrypts)
            var cursor = await messages.StoreEnvelopeAsync(envelope).ConfigureAwait(false);
            await discussions.IncrementSubmissionCountAsync(did).ConfigureAwait(false);

            // Publish domain event
            await eventBus.PublishAsync(new MessageAccepted
            {
                RoomId = rid,
                DiscussionId = did,
                MessageId = envelope.MessageId,
                SenderDeviceId = envelope.SenderDeviceId,
                MessageType = envelope.MessageType,
            }).ConfigureAwait(false);

            return Results.Ok(new { Cursor = cursor });
        });

        group.MapGet("/envelopes", async (
            Guid roomId,
            Guid discussionId,
            [FromQuery] long? after,
            IMessageStore messages,
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

            var response = await messages.GetEnvelopesAsync(
                rid,
                new DiscussionId(discussionId),
                after ?? 0).ConfigureAwait(false);
            return Results.Ok(response);
        });

        group.MapPost("/acknowledgements", (
            Guid roomId,
            Guid discussionId,
            [FromBody] AcknowledgementRequest request) =>
        {
            // Phase 1: acknowledgements are accepted but not durably tracked
            return Results.Ok(new { Acknowledged = request.Cursor });
        });

        group.MapPost("/cancel", async (
            Guid roomId,
            Guid discussionId,
            IDiscussionRepository discussions,
            IEventBus eventBus,
            HttpContext httpContext) =>
        {
            var did = new DiscussionId(discussionId);
            var discussion = await discussions.GetDiscussionAsync(did).ConfigureAwait(false);
            if (discussion is null)
            {
                return Results.NotFound("Discussion not found.");
            }

            if (discussion.Phase == DiscussionPhase.Closed)
            {
                return Results.BadRequest("Discussion is already closed.");
            }

            var previousPhase = discussion.Phase;
            var newPhase = DiscussionStateMachine.Transition(discussion.Phase, DiscussionTrigger.Cancel);
            await discussions.UpdateDiscussionStateAsync(did, newPhase, discussion.CurrentRound, null).ConfigureAwait(false);

            await eventBus.PublishAsync(new DiscussionClosed
            {
                RoomId = new RoomId(roomId),
                DiscussionId = did,
                FinalPhase = previousPhase,
            }).ConfigureAwait(false);

            return Results.Ok(new { Phase = newPhase.ToString() });
        });

        return group;
    }

    /// <summary>
    /// Phase 1 only: simple auth via X-Device-Id header.
    /// Returns true if the header matches the envelope sender.
    /// </summary>
    private static bool TryAuthenticateDevice(HttpContext context, DeviceId expectedDeviceId)
    {
        if (!context.Request.Headers.TryGetValue("X-Device-Id", out var deviceIdHeader))
        {
            return false;
        }

        if (!Guid.TryParse(deviceIdHeader.ToString(), out var parsedId))
        {
            return false;
        }

        return new DeviceId(parsedId) == expectedDeviceId;
    }
}

public sealed record AcknowledgementRequest
{
    public required long Cursor { get; init; }
}
