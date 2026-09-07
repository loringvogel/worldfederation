using Federation.Protocol;
using Federation.Sync;
using Xunit;

namespace Federation.NetworkPartition.Tests;

public sealed class EventLogTests
{
    private static MembershipChangedEvent CreateTestEvent()
    {
        return new MembershipChangedEvent
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
    }

    [Fact]
    public async Task OfflineSync_CopyEventsBetweenLogs_PreservesOrder()
    {
        var log1 = new InMemoryEventLog();
        var log2 = new InMemoryEventLog();

        // Append events to log1
        var event1 = CreateTestEvent();
        var event2 = CreateTestEvent();
        var event3 = CreateTestEvent();

        await log1.AppendAsync(event1);
        await log1.AppendAsync(event2);
        await log1.AppendAsync(event3);

        // Copy all events from log1 to log2
        var events = await log1.GetEventsAsync(null);
        foreach (var evt in events)
        {
            await log2.AppendAsync(evt);
        }

        // Verify order preserved in log2
        var log2Events = await log2.GetEventsAsync(null);
        Assert.Equal(3, log2Events.Count);
        Assert.Equal(event1.EventId, log2Events[0].EventId);
        Assert.Equal(event2.EventId, log2Events[1].EventId);
        Assert.Equal(event3.EventId, log2Events[2].EventId);
    }

    [Fact]
    public async Task DuplicateIdempotency_AppendSameEventTwice_StoredOnce()
    {
        var log = new InMemoryEventLog();
        var evt = CreateTestEvent();

        await log.AppendAsync(evt);
        await log.AppendAsync(evt); // duplicate

        var events = await log.GetEventsAsync(null);
        Assert.Single(events);
        Assert.Equal(evt.EventId, events[0].EventId);
    }

    [Fact]
    public async Task GetEventsAsync_WithAfterCursor_ReturnsOnlyNewerEvents()
    {
        var log = new InMemoryEventLog();

        var event1 = CreateTestEvent();
        var event2 = CreateTestEvent();
        var event3 = CreateTestEvent();

        await log.AppendAsync(event1);
        await log.AppendAsync(event2);
        await log.AppendAsync(event3);

        // Get events after event1
        var afterEvent1 = await log.GetEventsAsync(event1.EventId.ToString());
        Assert.Equal(2, afterEvent1.Count);
        Assert.Equal(event2.EventId, afterEvent1[0].EventId);
        Assert.Equal(event3.EventId, afterEvent1[1].EventId);

        // Get events after event2
        var afterEvent2 = await log.GetEventsAsync(event2.EventId.ToString());
        Assert.Single(afterEvent2);
        Assert.Equal(event3.EventId, afterEvent2[0].EventId);

        // Get events after event3 (last event)
        var afterEvent3 = await log.GetEventsAsync(event3.EventId.ToString());
        Assert.Empty(afterEvent3);
    }

    [Fact]
    public async Task GetEventAsync_ExistingEvent_ReturnsEvent()
    {
        var log = new InMemoryEventLog();
        var evt = CreateTestEvent();

        await log.AppendAsync(evt);

        var retrieved = await log.GetEventAsync(evt.EventId.ToString());
        Assert.NotNull(retrieved);
        Assert.Equal(evt.EventId, retrieved.EventId);
    }

    [Fact]
    public async Task GetEventAsync_NonExistentEvent_ReturnsNull()
    {
        var log = new InMemoryEventLog();

        var retrieved = await log.GetEventAsync(Guid.NewGuid().ToString());
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetEventsAsync_NullCursor_ReturnsAllEvents()
    {
        var log = new InMemoryEventLog();

        var event1 = CreateTestEvent();
        var event2 = CreateTestEvent();

        await log.AppendAsync(event1);
        await log.AppendAsync(event2);

        var all = await log.GetEventsAsync(null);
        Assert.Equal(2, all.Count);
    }
}
