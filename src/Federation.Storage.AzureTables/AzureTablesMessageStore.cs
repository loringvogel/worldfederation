using Azure;
using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesMessageStore : IMessageStore
{
    private readonly TableClient _envelopesTable;
    private readonly TableClient _envelopeIdsTable;

    public AzureTablesMessageStore(TableServiceClient serviceClient)
    {
        _envelopesTable = serviceClient.GetTableClient("envelopes");
        _envelopesTable.CreateIfNotExists();

        _envelopeIdsTable = serviceClient.GetTableClient("envelopeids");
        _envelopeIdsTable.CreateIfNotExists();
    }

    public async Task<long> StoreEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        var cursor = DateTimeOffset.UtcNow.Ticks;
        var pk = envelope.RoomId.Value.ToString("N") + "_" + envelope.DiscussionId.Value.ToString("N");
        var rk = cursor.ToString("D20") + "_" + envelope.MessageId.Value.ToString("N");

        var entity = new TableEntity(pk, rk)
        {
            ["ProtocolVersion"] = envelope.ProtocolVersion,
            ["RoomId"] = envelope.RoomId.Value.ToString("N"),
            ["DiscussionId"] = envelope.DiscussionId.Value.ToString("N"),
            ["Epoch"] = envelope.Epoch.Value,
            ["MessageId"] = envelope.MessageId.Value.ToString("N"),
            ["SenderDeviceId"] = envelope.SenderDeviceId.Value.ToString("N"),
            ["SenderSequence"] = envelope.SenderSequence,
            ["MessageType"] = envelope.MessageType.ToString(),
            ["Round"] = envelope.Round,
            ["CreatedAt"] = envelope.CreatedAt.ToString("O"),
            ["ExpiresAt"] = envelope.ExpiresAt.ToString("O"),
            ["CipherSuite"] = envelope.CipherSuite,
            ["Ciphertext"] = Convert.ToBase64String(envelope.Ciphertext),
            ["Signature"] = Convert.ToBase64String(envelope.Signature),
            ["Cursor"] = cursor
        };

        await _envelopesTable.AddEntityAsync(entity, ct).ConfigureAwait(false);

        // Secondary lookup: envelopeids table
        var idLookup = new TableEntity("all", envelope.MessageId.Value.ToString("N"))
        {
            ["Cursor"] = cursor.ToString()
        };
        await _envelopeIdsTable.UpsertEntityAsync(idLookup, TableUpdateMode.Replace, ct).ConfigureAwait(false);

        return cursor;
    }

    public async Task<GetEnvelopesResponse> GetEnvelopesAsync(
        RoomId roomId, DiscussionId discussionId, long afterCursor, CancellationToken ct)
    {
        var pk = roomId.Value.ToString("N") + "_" + discussionId.Value.ToString("N");
        var afterRk = afterCursor.ToString("D20");

        var filter = $"PartitionKey eq '{pk}' and RowKey gt '{afterRk}'";
        var envelopes = new List<MessageEnvelope>();
        long lastCursor = afterCursor;

        var pages = _envelopesTable.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            var envelope = ToEnvelope(entity);
            envelopes.Add(envelope);
            var entityCursor = entity.GetInt64("Cursor") ?? 0L;
            if (entityCursor > lastCursor)
                lastCursor = entityCursor;
        }

        return new GetEnvelopesResponse { Envelopes = envelopes, Cursor = lastCursor };
    }

    public async Task<bool> ExistsAsync(MessageId messageId, CancellationToken ct)
    {
        try
        {
            await _envelopeIdsTable.GetEntityAsync<TableEntity>(
                "all", messageId.Value.ToString("N"), cancellationToken: ct).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static MessageEnvelope ToEnvelope(TableEntity e)
    {
        var protocolVersion = e.GetString("ProtocolVersion") ?? string.Empty;
        var roomId = new RoomId(Guid.Parse(e.GetString("RoomId") ?? string.Empty));
        var discussionId = new DiscussionId(Guid.Parse(e.GetString("DiscussionId") ?? string.Empty));
        var epoch = new EpochId(e.GetInt64("Epoch") ?? 0L);
        var messageId = new MessageId(Guid.Parse(e.GetString("MessageId") ?? string.Empty));
        var senderDeviceId = new DeviceId(Guid.Parse(e.GetString("SenderDeviceId") ?? string.Empty));
        var senderSequence = e.GetInt64("SenderSequence") ?? 0L;
        var messageType = Enum.Parse<MessageType>(e.GetString("MessageType") ?? "Proposal");
        var round = e.GetInt32("Round") ?? 0;
        var createdAt = DateTimeOffset.Parse(e.GetString("CreatedAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var expiresAt = DateTimeOffset.Parse(e.GetString("ExpiresAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var cipherSuite = e.GetString("CipherSuite") ?? string.Empty;
        var ciphertext = Convert.FromBase64String(e.GetString("Ciphertext") ?? string.Empty);
        var signature = Convert.FromBase64String(e.GetString("Signature") ?? string.Empty);

        return new MessageEnvelope { ProtocolVersion = protocolVersion, RoomId = roomId, DiscussionId = discussionId, Epoch = epoch, MessageId = messageId, SenderDeviceId = senderDeviceId, SenderSequence = senderSequence, MessageType = messageType, Round = round, CreatedAt = createdAt, ExpiresAt = expiresAt, CipherSuite = cipherSuite, Ciphertext = ciphertext, Signature = signature };
    }
}
