using Federation.Protocol;
using Federation.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Federation.NetworkPartition.Tests;

public sealed class SyncEngineTests
{
    [Fact]
    public async Task SyncEngine_CanBeConstructedAndStarted()
    {
        var localLog = new InMemoryEventLog();
        var engine = new SyncEngine(localLog, NullLogger<SyncEngine>.Instance);

        await engine.StartAsync(CancellationToken.None);
        await engine.StopAsync(CancellationToken.None);

        // No exception means success
        Assert.Empty(engine.QuarantinedEvents);
    }

    [Fact]
    public async Task SyncFromAsync_ValidEvents_AppendedToLocalLog()
    {
        var localLog = new InMemoryEventLog();
        var remoteLog = new InMemoryEventLog();
        var engine = new SyncEngine(localLog, NullLogger<SyncEngine>.Instance);

        var evt = new MembershipChangedEvent
        {
            EventType = "MembershipChanged",
            Signature = new byte[] { 1, 2, 3 },
            Payload = new MembershipChangedPayload
            {
                RoomId = RoomId.New(),
                DeviceId = DeviceId.New(),
                MembershipId = MembershipId.New(),
            },
        };

        await remoteLog.AppendAsync(evt);

        // Sync with always-valid signature check
        await engine.SyncFromAsync(remoteLog, null, _ => true);

        var localEvents = await localLog.GetEventsAsync(null);
        Assert.Single(localEvents);
        Assert.Equal(evt.EventId, localEvents[0].EventId);
    }

    [Fact]
    public async Task SyncFromAsync_InvalidSignature_EventQuarantined()
    {
        var localLog = new InMemoryEventLog();
        var remoteLog = new InMemoryEventLog();
        var engine = new SyncEngine(localLog, NullLogger<SyncEngine>.Instance);

        var evt = new MembershipChangedEvent
        {
            EventType = "MembershipChanged",
            Signature = new byte[] { 1, 2, 3 },
            Payload = new MembershipChangedPayload
            {
                RoomId = RoomId.New(),
                DeviceId = DeviceId.New(),
                MembershipId = MembershipId.New(),
            },
        };

        await remoteLog.AppendAsync(evt);

        // Sync with always-invalid signature check
        await engine.SyncFromAsync(remoteLog, null, _ => false);

        // Event should NOT be in local log
        var localEvents = await localLog.GetEventsAsync(null);
        Assert.Empty(localEvents);

        // Event should be quarantined
        Assert.Single(engine.QuarantinedEvents);
        Assert.Equal(evt.EventId, engine.QuarantinedEvents[0].EventId);
    }

    [Fact]
    public async Task SyncFromAsync_WithAfterEventId_OnlySyncsNewerEvents()
    {
        var localLog = new InMemoryEventLog();
        var remoteLog = new InMemoryEventLog();
        var engine = new SyncEngine(localLog, NullLogger<SyncEngine>.Instance);

        var event1 = new MembershipChangedEvent
        {
            EventType = "MembershipChanged",
            Signature = new byte[] { 1 },
            Payload = new MembershipChangedPayload
            {
                RoomId = RoomId.New(),
                DeviceId = DeviceId.New(),
                MembershipId = MembershipId.New(),
            },
        };
        var event2 = new MembershipChangedEvent
        {
            EventType = "MembershipChanged",
            Signature = new byte[] { 2 },
            Payload = new MembershipChangedPayload
            {
                RoomId = RoomId.New(),
                DeviceId = DeviceId.New(),
                MembershipId = MembershipId.New(),
            },
        };

        await remoteLog.AppendAsync(event1);
        await remoteLog.AppendAsync(event2);

        // Sync only events after event1
        await engine.SyncFromAsync(remoteLog, event1.EventId.ToString(), _ => true);

        var localEvents = await localLog.GetEventsAsync(null);
        Assert.Single(localEvents);
        Assert.Equal(event2.EventId, localEvents[0].EventId);
    }
}
