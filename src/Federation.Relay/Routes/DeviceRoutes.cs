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
            return Results.Created($"/v1/devices/{registration.DeviceId}", registration);
        });

        group.MapDelete("/{deviceId}", async (
            Guid deviceId,
            IDeviceRepository devices) =>
        {
            await devices.RemoveDeviceAsync(new DeviceId(deviceId)).ConfigureAwait(false);
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record RegisterDeviceRequest
{
    public required string DisplayName { get; init; }
}
