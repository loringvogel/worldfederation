using Federation.Protocol;
using Federation.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Federation.Relay.Routes;

public static class DeviceRoutes
{
    public static RouteGroupBuilder MapDeviceRoutes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/devices");

        group.MapPost("/registrations", async (
            [FromBody] RegisterDeviceRequest request,
            IDeviceRepository devices,
            ICryptoProvider crypto) =>
        {
            var (publicKey, _) = crypto.GenerateDeviceKeyPair();
            var registration = await devices.RegisterDeviceAsync(request.DisplayName, publicKey).ConfigureAwait(false);
            var response = new DeviceRegistrationApiResponse(
                registration.DeviceId.Value.ToString("D"),
                registration.DisplayName,
                registration.DeviceToken,
                registration.RegisteredAt,
                publicKey);
            return Results.Created($"/v1/devices/{registration.DeviceId}", response);
        });

        group.MapGet("/", async (IDeviceRepository devices) =>
        {
            var all = await devices.ListAllAsync().ConfigureAwait(false);
            var response = all.Select(d => new DeviceRegistrationApiResponse(
                d.DeviceId.Value.ToString("D"),
                d.DisplayName,
                d.DeviceToken,
                d.RegisteredAt,
                d.PublicKey)).ToList();
            return Results.Ok(response);
        });

        group.MapDelete("/{deviceId}", async (
            Guid deviceId,
            IDeviceRepository devices) =>
        {
            await devices.RemoveDeviceAsync(new DeviceId(deviceId)).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/{deviceId}/emergency-revoke", async (
            Guid deviceId,
            IMembershipRepository memberships,
            ISecurityEventStore securityEvents) =>
        {
            var did = new DeviceId(deviceId);

            // Revoke device from ALL rooms in one call
            var roomIds = await memberships.GetRoomsForDeviceAsync(did).ConfigureAwait(false);
            foreach (var roomId in roomIds)
            {
                var membership = await memberships.GetMembershipByDeviceAsync(roomId, did).ConfigureAwait(false);
                if (membership is not null)
                {
                    await memberships.RevokeMembershipAsync(membership.MembershipId).ConfigureAwait(false);
                }
            }

            await securityEvents.AppendAsync(new SecurityEvent
            {
                OccurredAt = DateTimeOffset.UtcNow,
                EventType = "EmergencyRevoke",
                Description = $"Emergency revocation of device {did} from {roomIds.Count} room(s).",
                DeviceId = did,
            }).ConfigureAwait(false);

            return Results.Ok(new { RevokedFromRooms = roomIds.Count });
        });

        return group;
    }
}

public sealed record RegisterDeviceRequest
{
    public required string DisplayName { get; init; }
}

/// <summary>Wire response for device registration — uses plain strings to avoid strong-ID serialization issues.</summary>
#pragma warning disable CA1819 // DTO: byte[] needed for base64 wire serialization of public key
public sealed record DeviceRegistrationApiResponse(string DeviceId, string DisplayName, string DeviceToken, DateTimeOffset RegisteredAt, byte[] PublicKey);
#pragma warning restore CA1819
