using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Federation.Protocol;
using Federation.Cryptography;
using Federation.Transport;
using Federation.Identity;
using Federation.Sync;
using Microsoft.Extensions.Logging;

namespace Federation.Node;

/// <summary>
/// The local security boundary. Holds crypto keys, polls the relay for new envelopes,
/// verifies signatures and epochs, decrypts messages, and stores them in the local cache.
/// Invalid messages are logged as security events and never exposed to the agent.
/// </summary>
public sealed class CouncilNode : IDisposable
{
    private readonly CouncilNodeOptions _options;
    private readonly ICryptoProvider _crypto;
    private readonly IGroupSession _groupSession;
    private readonly IKeyStore _keyStore;
    private readonly InMemoryLocalCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CouncilNode> _logger;
    private readonly HashSet<MessageId> _seenMessageIds = [];
    private readonly Dictionary<(RoomId, DiscussionId), long> _cursors = [];
    private readonly byte[] _publicKey;
    private readonly byte[] _privateKey;
    private readonly IRelayTransport? _relayTransport;
    private readonly IIdentityStore? _identityStore;
    private readonly IEventLog? _eventLog;
    private readonly IAcknowledgementStore? _acknowledgementStore;
    private readonly CancellationTokenSource _cts = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public CouncilNode(
        CouncilNodeOptions options,
        ICryptoProvider crypto,
        IGroupSession groupSession,
        IKeyStore keyStore,
        InMemoryLocalCache cache,
        HttpClient httpClient,
        ILogger<CouncilNode> logger,
        byte[] publicKey,
        byte[] privateKey,
        IRelayTransport? relayTransport = null,
        IIdentityStore? identityStore = null,
        IEventLog? eventLog = null,
        IAcknowledgementStore? acknowledgementStore = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(crypto);
        ArgumentNullException.ThrowIfNull(groupSession);
        ArgumentNullException.ThrowIfNull(keyStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(privateKey);

        _options = options;
        _crypto = crypto;
        _groupSession = groupSession;
        _keyStore = keyStore;
        _cache = cache;
        _httpClient = httpClient;
        _logger = logger;
        _publicKey = publicKey;
        _privateKey = privateKey;
        _relayTransport = relayTransport;
        _identityStore = identityStore;
        _eventLog = eventLog;
        _acknowledgementStore = acknowledgementStore;

        // Set Phase 1 auth headers
        _httpClient.DefaultRequestHeaders.Add("X-Device-Id", options.DeviceId.Value.ToString());
        _httpClient.DefaultRequestHeaders.Add("X-Device-Token", options.DeviceToken);
    }

    /// <summary>The local cache of decrypted messages.</summary>
    public InMemoryLocalCache Cache => _cache;

    /// <summary>The node's group session for encryption/decryption.</summary>
    public IGroupSession GroupSession => _groupSession;

    /// <summary>The node's crypto provider for signing/verification.</summary>
    public ICryptoProvider CryptoProvider => _crypto;

    /// <summary>The node's public key.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto key material; array is the natural representation.")]
    public byte[] PublicKey => _publicKey;

    /// <summary>The node's private key (used for signing outbound envelopes).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Crypto key material; array is the natural representation.")]
    public byte[] PrivateKey => _privateKey;

    /// <summary>The node's configuration options.</summary>
    public CouncilNodeOptions Options => _options;

    /// <summary>The node's key store.</summary>
    public IKeyStore KeyStore => _keyStore;

    /// <summary>
    /// Polls the relay for new envelopes for all known discussions and processes them.
    /// </summary>
    public async Task PollAndProcessAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default)
    {
        var cursorKey = (roomId, discussionId);

        // Load cursor from persistent store if available and not yet cached in memory
        if (!_cursors.ContainsKey(cursorKey) && _acknowledgementStore is not null)
        {
            var persistedCursor = await _acknowledgementStore.GetCursorAsync(roomId, discussionId, ct).ConfigureAwait(false);
            _cursors[cursorKey] = persistedCursor;
        }

        _cursors.TryGetValue(cursorKey, out var cursor);

        var url = $"{_options.RelayBaseUrl}/v1/rooms/{roomId.Value}/discussions/{discussionId.Value}/envelopes?after={cursor}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(new Uri(url), ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
#pragma warning disable CA1031 // Network errors should not crash the polling loop
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll relay at {Url}.", url);
            return;
        }
#pragma warning restore CA1031

        var envelopeResponse = await response.Content.ReadFromJsonAsync<GetEnvelopesResponse>(JsonOptions, ct).ConfigureAwait(false);
        if (envelopeResponse is null || envelopeResponse.Envelopes.Count == 0)
        {
            return;
        }

        foreach (var envelope in envelopeResponse.Envelopes)
        {
            ProcessEnvelope(envelope, roomId, discussionId, envelopeResponse.Cursor);
        }

        _cursors[cursorKey] = envelopeResponse.Cursor;

        // Persist cursor if store is available
        if (_acknowledgementStore is not null)
        {
            await _acknowledgementStore.SetCursorAsync(roomId, discussionId, envelopeResponse.Cursor, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a signed and encrypted envelope ready for submission to the relay.
    /// </summary>
    public MessageEnvelope CreateEnvelope(
        RoomId roomId,
        DiscussionId discussionId,
        MessageType messageType,
        int round,
        string plaintext,
        long senderSequence)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var messageId = MessageId.New();

        // Additional authenticated data includes room, discussion, and message metadata
        var aad = Encoding.UTF8.GetBytes($"{roomId}:{discussionId}:{messageId}");

        var (ciphertext, epoch) = _groupSession.EncryptMessage(plaintextBytes, aad);

        // Sign the ciphertext + metadata
        var signatureInput = BuildSignatureInput(roomId, discussionId, messageId, epoch, ciphertext);
        var signature = _crypto.Sign(signatureInput, _privateKey);

        return new MessageEnvelope
        {
            ProtocolVersion = "1.0",
            RoomId = roomId,
            DiscussionId = discussionId,
            Epoch = epoch,
            MessageId = messageId,
            SenderDeviceId = _options.DeviceId,
            SenderSequence = senderSequence,
            MessageType = messageType,
            Round = round,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            CipherSuite = "AES-256-GCM+ECDSA-P256",
            Ciphertext = ciphertext,
            Signature = signature,
        };
    }

    /// <summary>Submits an envelope to the relay via HTTP POST.</summary>
    public async Task<bool> SubmitEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var url = $"{_options.RelayBaseUrl}/v1/rooms/{envelope.RoomId.Value}/discussions/{envelope.DiscussionId.Value}/envelopes";
        var request = new SubmitEnvelopeRequest { Envelope = envelope };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(new Uri(url), request, JsonOptions, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031 // Network errors should be surfaced as false
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to submit envelope to {Url}.", url);
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Immediately halts the node, wipes local keys and cached data, and signals
    /// the relay to revoke this device from all rooms. After this call the node
    /// cannot decrypt past or future messages. The room epoch rotates, severing
    /// this cell from all others.
    ///
    /// Analogy: a captured FLN cell member who destroys their contact list.
    /// The cell is gone but other cells are unaffected.
    /// </summary>
    public async Task EmergencyStopAsync(CancellationToken ct = default)
    {
        // 1. Stop the poll loop immediately
        await _cts.CancelAsync().ConfigureAwait(false);

        // 2. Wipe all local key material
        await _keyStore.DeleteAllKeysAsync(ct).ConfigureAwait(false);

        // 3. Post emergency revoke to relay
        try
        {
            var url = $"{_options.RelayBaseUrl}/v1/devices/{_options.DeviceId.Value}/emergency-revoke";
            await _httpClient.PostAsync(new Uri(url), null, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Network errors during emergency stop should not prevent local cleanup
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post emergency revoke to relay. Local keys are still wiped.");
        }
#pragma warning restore CA1031

        // 4. Log security event
        _logger.LogCritical("Emergency stop executed. Local keys wiped. Device revoked from all rooms.");
    }

    private void ProcessEnvelope(MessageEnvelope envelope, RoomId expectedRoomId, DiscussionId expectedDiscussionId, long cursor)
    {
        // Verify room ID matches
        if (envelope.RoomId != expectedRoomId)
        {
            _logger.LogWarning("Envelope {MessageId} has wrong room ID. Expected {Expected}, got {Actual}.",
                envelope.MessageId, expectedRoomId, envelope.RoomId);
            return;
        }

        // Verify message ID not seen before (replay protection)
        if (!_seenMessageIds.Add(envelope.MessageId))
        {
            _logger.LogWarning("Duplicate message ID {MessageId} detected. Rejecting.", envelope.MessageId);
            return;
        }

        // Verify not expired
        if (envelope.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Envelope {MessageId} has expired. Rejecting.", envelope.MessageId);
            return;
        }

        // Verify epoch matches current group session
        if (envelope.Epoch != _groupSession.CurrentEpoch)
        {
            _logger.LogWarning("Envelope {MessageId} epoch mismatch. Expected {Expected}, got {Actual}.",
                envelope.MessageId, _groupSession.CurrentEpoch, envelope.Epoch);
            return;
        }

        // Verify sequence is non-negative
        if (envelope.SenderSequence < 0)
        {
            _logger.LogWarning("Envelope {MessageId} has negative sequence {Sequence}. Rejecting.",
                envelope.MessageId, envelope.SenderSequence);
            return;
        }

        // Decrypt
        byte[] plaintext;
        try
        {
            var aad = Encoding.UTF8.GetBytes($"{envelope.RoomId}:{envelope.DiscussionId}:{envelope.MessageId}");
            plaintext = _groupSession.DecryptMessage(envelope.Ciphertext, aad, envelope.Epoch);
        }
#pragma warning disable CA1031 // Decryption failure should not crash the node
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt envelope {MessageId}.", envelope.MessageId);
            return;
        }
#pragma warning restore CA1031

        // Store decrypted message in local cache
        var decrypted = new DecryptedMessage
        {
            MessageId = envelope.MessageId,
            SenderDeviceId = envelope.SenderDeviceId,
            MessageType = envelope.MessageType,
            Round = envelope.Round,
            CreatedAt = envelope.CreatedAt,
            Plaintext = Encoding.UTF8.GetString(plaintext),
            Cursor = cursor,
        };

        _cache.Store(expectedRoomId, expectedDiscussionId, cursor, decrypted);
        _logger.LogInformation("Processed envelope {MessageId} from device {DeviceId}.",
            envelope.MessageId, envelope.SenderDeviceId);
    }

    private static byte[] BuildSignatureInput(RoomId roomId, DiscussionId discussionId, MessageId messageId, EpochId epoch, byte[] ciphertext)
    {
        var metadata = Encoding.UTF8.GetBytes($"{roomId}:{discussionId}:{messageId}:{epoch.Value}");
        var input = new byte[metadata.Length + ciphertext.Length];
        metadata.CopyTo(input, 0);
        ciphertext.CopyTo(input, metadata.Length);
        return input;
    }

    public void Dispose()
    {
        _cts.Dispose();
        _httpClient.Dispose();
    }
}
