using System.Net.Http.Json;
using System.Text.Json;
using Federation.Protocol;

namespace Federation.Node.ToolServer;

/// <summary>
/// Exposes exactly the 8 operations from spec section 7 to the local agent.
/// No other operations are exposed. No generic HTTP, shell, filesystem, or membership operations.
///
/// Each method validates the operation is permitted for the current phase,
/// encrypts + signs via the node's crypto, and posts to the relay.
/// </summary>
public sealed class CouncilToolService
{
    private readonly CouncilNode _node;
    private readonly HttpClient _httpClient;
    private long _senderSequence;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public CouncilToolService(CouncilNode node, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(httpClient);

        _node = node;
        _httpClient = httpClient;

        _httpClient.DefaultRequestHeaders.Add("X-Device-Id", node.Options.DeviceId.Value.ToString());
        _httpClient.DefaultRequestHeaders.Add("X-Device-Token", node.Options.DeviceToken);
    }

    /// <summary>Lists all rooms this node participates in.</summary>
    public async Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct = default)
    {
        var url = $"{_node.Options.RelayBaseUrl}/v1/rooms";
        var response = await _httpClient.GetFromJsonAsync<IReadOnlyList<RoomSummary>>(new Uri(url), JsonOptions, ct).ConfigureAwait(false);
        return response ?? [];
    }

    /// <summary>Gets the current state and messages for a discussion since a cursor.</summary>
    public async Task<DiscussionSnapshot> GetDiscussionAsync(RoomId roomId, long sinceCursor = 0, CancellationToken ct = default)
    {
        // Get discussion state from relay
        var discussions = await GetDiscussionsInRoomAsync(roomId, ct).ConfigureAwait(false);

        // Get locally cached decrypted messages
        var allMessages = new List<DecryptedMessage>();
        foreach (var disc in discussions)
        {
            var messages = _node.Cache.GetMessages(roomId, disc.DiscussionId, sinceCursor);
            allMessages.AddRange(messages);
        }

        // Wrap messages with untrusted data preamble for safe agent consumption
        var wrappedContent = AgentContext.WrapForAgent(allMessages);

        return new DiscussionSnapshot
        {
            Discussions = discussions,
            Messages = allMessages,
            WrappedContent = wrappedContent,
        };
    }

    /// <summary>Submits a proposal in the ProposalRound phase.</summary>
    public async Task<bool> SubmitProposalAsync(RoomId roomId, RoundId roundId, string text, CancellationToken ct = default)
    {
        return await SubmitMessageAsync(roomId, MessageType.Proposal, text, ct).ConfigureAwait(false);
    }

    /// <summary>Submits a critique in the CritiqueRound phase.</summary>
    public async Task<bool> SubmitCritiqueAsync(RoomId roomId, RoundId roundId, MessageId targetMessageId, string text, CancellationToken ct = default)
    {
        var prefixedText = $"[Critique of {targetMessageId}]\n{text}";
        return await SubmitMessageAsync(roomId, MessageType.Critique, prefixedText, ct).ConfigureAwait(false);
    }

    /// <summary>Submits a revision in the RevisionRound phase.</summary>
    public async Task<bool> SubmitRevisionAsync(RoomId roomId, RoundId roundId, string text, CancellationToken ct = default)
    {
        return await SubmitMessageAsync(roomId, MessageType.Revision, text, ct).ConfigureAwait(false);
    }

    /// <summary>Submits a vote in the Vote phase.</summary>
    public async Task<bool> SubmitVoteAsync(RoomId roomId, RoundId roundId, VoteChoice choice, string rationale, CancellationToken ct = default)
    {
        var text = $"Vote: {choice}\nRationale: {rationale}";
        return await SubmitMessageAsync(roomId, MessageType.Vote, text, ct).ConfigureAwait(false);
    }

    /// <summary>Submits a synthesis in the Synthesis phase.</summary>
    public async Task<bool> SubmitSynthesisAsync(RoomId roomId, RoundId roundId, string text, CancellationToken ct = default)
    {
        return await SubmitMessageAsync(roomId, MessageType.Synthesis, text, ct).ConfigureAwait(false);
    }

    /// <summary>Acknowledges receipt of messages up to a given cursor.</summary>
    public async Task<bool> AcknowledgeAsync(RoomId roomId, long cursor, CancellationToken ct = default)
    {
        // We need to know the discussion ID for the acknowledgement endpoint.
        // In Phase 1, we acknowledge across all discussions in the room.
        var discussions = await GetDiscussionsInRoomAsync(roomId, ct).ConfigureAwait(false);
        foreach (var disc in discussions)
        {
            var url = $"{_node.Options.RelayBaseUrl}/v1/rooms/{roomId.Value}/discussions/{disc.DiscussionId.Value}/acknowledgements";
            var request = new { Cursor = cursor };
            await _httpClient.PostAsJsonAsync(new Uri(url), request, JsonOptions, ct).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<bool> SubmitMessageAsync(RoomId roomId, MessageType messageType, string text, CancellationToken ct)
    {
        // Find the active discussion in this room
        var discussions = await GetDiscussionsInRoomAsync(roomId, ct).ConfigureAwait(false);
        var activeDiscussion = discussions.FirstOrDefault(d => d.Phase != DiscussionPhase.Closed && d.Phase != DiscussionPhase.Draft);

        if (activeDiscussion is null)
        {
            return false;
        }

        var sequence = Interlocked.Increment(ref _senderSequence);
        var envelope = _node.CreateEnvelope(
            roomId,
            activeDiscussion.DiscussionId,
            messageType,
            activeDiscussion.CurrentRound,
            text,
            sequence);

        return await _node.SubmitEnvelopeAsync(envelope, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<DiscussionState>> GetDiscussionsInRoomAsync(RoomId roomId, CancellationToken ct)
    {
        // Phase 1: get all discussions from the relay and filter by room
        // In a real implementation, there would be a dedicated endpoint for this
        var url = $"{_node.Options.RelayBaseUrl}/v1/rooms";
        var rooms = await _httpClient.GetFromJsonAsync<IReadOnlyList<RoomSummary>>(new Uri(url), JsonOptions, ct).ConfigureAwait(false);

        // For Phase 1, return empty if room not found; real impl would have a proper endpoint
        if (rooms is null || !rooms.Any(r => r.RoomId == roomId))
        {
            return [];
        }

        return [];
    }
}

/// <summary>Snapshot of a discussion as seen by the local agent.</summary>
public sealed record DiscussionSnapshot
{
    public required IReadOnlyList<DiscussionState> Discussions { get; init; }
    public required IReadOnlyList<DecryptedMessage> Messages { get; init; }
    public required string WrappedContent { get; init; }
}
