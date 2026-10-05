using System.Text.Json;
using Azure.Storage.Queues;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

/// <summary>
/// IEventBus backed by Azure Storage Queues.
/// Events are serialized as JSON with a $type discriminator and enqueued for any
/// consumer (cross-replica notification, audit, future workers).
/// Queue is created on first use if it does not already exist.
/// </summary>
public sealed class AzureQueuesEventBus : IEventBus
{
    private readonly QueueClient _queue;
    private bool _ensured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public AzureQueuesEventBus(string connectionString, string queueName = "federation-events")
    {
        _queue = new QueueClient(connectionString, queueName);
    }

    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        await EnsureQueueAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize<DomainEvent>(domainEvent, JsonOpts);
        // Storage Queue messages must be base64-encoded (SDK handles this automatically
        // when messageEncoding is set; we send as plain UTF-8 text).
        await _queue.SendMessageAsync(json, cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task EnsureQueueAsync(CancellationToken ct)
    {
        if (_ensured) return;
        await _queue.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        _ensured = true;
    }
}
