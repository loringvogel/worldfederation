using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Federation.Cryptography;
using Federation.Identity;
using Federation.Node;
using Federation.Protocol;
using Federation.Relay.Stores;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Federation.Security.Tests;

public sealed class CellCompartmentalizationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<CouncilNode> _nodes = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public CellCompartmentalizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPeersForRoom_DoesNotReturnPeersFromOtherRooms()
    {
        var store = new InMemoryIdentityStore();
        var roomA = RoomId.New();
        var roomB = RoomId.New();

        var peer1 = CreatePeerRecord(DeviceId.New());
        var peer2 = CreatePeerRecord(DeviceId.New());

        await store.StorePeerRecordForRoomAsync(roomA, peer1);
        await store.StorePeerRecordForRoomAsync(roomB, peer2);

        var peersInA = await store.GetPeersForRoomAsync(roomA);
        var peersInB = await store.GetPeersForRoomAsync(roomB);

        Assert.Single(peersInA);
        Assert.Equal(peer1.DeviceId, peersInA[0].DeviceId);
        Assert.DoesNotContain(peersInA, p => p.DeviceId == peer2.DeviceId);

        Assert.Single(peersInB);
        Assert.Equal(peer2.DeviceId, peersInB[0].DeviceId);
        Assert.DoesNotContain(peersInB, p => p.DeviceId == peer1.DeviceId);
    }

    [Fact]
    public async Task RelayRoomList_OnlyReturnsDeviceMemberRooms()
    {
        var client = _factory.CreateClient();

        // Register two devices
        var response = await client.PostAsJsonAsync("/v1/devices/registrations", new { DisplayName = "Compartment-Agent" }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var device = await response.Content.ReadFromJsonAsync<DeviceRegistration>(JsonOptions);
        Assert.NotNull(device);

        var response2 = await client.PostAsJsonAsync("/v1/devices/registrations", new { DisplayName = "Other-Agent" }, JsonOptions);
        response2.EnsureSuccessStatusCode();
        var otherDevice = await response2.Content.ReadFromJsonAsync<DeviceRegistration>(JsonOptions);
        Assert.NotNull(otherDevice);

        // Create room A with our device as owner (device automatically becomes a member)
        var roomAResp = await client.PostAsJsonAsync("/v1/rooms",
            new CreateRoomRequest { Name = "Room A", OwnerDeviceId = device.DeviceId }, JsonOptions);
        roomAResp.EnsureSuccessStatusCode();
        var roomA = await roomAResp.Content.ReadFromJsonAsync<RoomSummary>(JsonOptions);
        Assert.NotNull(roomA);

        // Create room B with a DIFFERENT device as owner (our device is NOT a member)
        var roomBResp = await client.PostAsJsonAsync("/v1/rooms",
            new CreateRoomRequest { Name = "Room B", OwnerDeviceId = otherDevice.DeviceId }, JsonOptions);
        roomBResp.EnsureSuccessStatusCode();
        var roomB = await roomBResp.Content.ReadFromJsonAsync<RoomSummary>(JsonOptions);
        Assert.NotNull(roomB);

        // GET /v1/rooms with X-Device-Id header
        using var listMsg = new HttpRequestMessage(HttpMethod.Get, "/v1/rooms");
        listMsg.Headers.Add("X-Device-Id", device.DeviceId.Value.ToString());
        var listResp = await client.SendAsync(listMsg);
        listResp.EnsureSuccessStatusCode();

        var rooms = await listResp.Content.ReadFromJsonAsync<List<RoomSummary>>(JsonOptions);
        Assert.NotNull(rooms);
        Assert.Contains(rooms, r => r.RoomId == roomA.RoomId);
        Assert.DoesNotContain(rooms, r => r.RoomId == roomB.RoomId);
    }

    [Fact]
    public async Task EmergencyStop_WipesLocalKeys()
    {
        var crypto = new InMemoryCryptoProvider();
        var (publicKey, privateKey) = crypto.GenerateDeviceKeyPair();
        var keyStore = new InMemoryKeyStore();

        // Store some keys
        var deviceId = DeviceId.New();
        await keyStore.StoreDeviceKeyAsync(deviceId, privateKey);
        await keyStore.StoreEpochKeyAsync(new EpochId(1), new byte[] { 1, 2, 3 });

        // Verify keys exist
        Assert.NotNull(await keyStore.LoadDeviceKeyAsync(deviceId));
        Assert.NotNull(await keyStore.LoadEpochKeyAsync(new EpochId(1)));

        var session = new InMemoryGroupSession();
        var options = new CouncilNodeOptions
        {
            RelayBaseUrl = "http://localhost:9999", // non-existent, that's fine
            DeviceId = deviceId,
            DeviceToken = "test-token",
            RoomIds = [],
        };

        var httpClient = new HttpClient();
        var node = new CouncilNode(
            options, crypto, session, keyStore,
            new InMemoryLocalCache(), httpClient,
            NullLogger<CouncilNode>.Instance,
            publicKey, privateKey);

        _nodes.Add(node);

        // Execute emergency stop
        await node.EmergencyStopAsync();

        // Verify keys are wiped
        Assert.Null(await keyStore.LoadDeviceKeyAsync(deviceId));
        Assert.Null(await keyStore.LoadEpochKeyAsync(new EpochId(1)));
    }

    private static PeerRecord CreatePeerRecord(DeviceId deviceId)
    {
        return new PeerRecord
        {
            DeviceId = deviceId,
            HumanId = HumanId.New(),
            Addresses = ["tcp://127.0.0.1:7777"],
            SupportedProtocolVersions = ["1.0"],
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            Capabilities = ["council"],
            Signature = new byte[] { 0x01, 0x02, 0x03 },
        };
    }

    public void Dispose()
    {
        foreach (var node in _nodes)
        {
            node.Dispose();
        }
    }
}
