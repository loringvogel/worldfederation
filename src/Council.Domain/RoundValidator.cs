using Council.Contracts;

namespace Council.Domain;

/// <summary>
/// Validates that a message submission is permitted for the current discussion phase and round.
/// </summary>
public static class RoundValidator
{
    private static readonly Dictionary<DiscussionPhase, MessageType> AllowedMessageTypes = new()
    {
        [DiscussionPhase.ProposalRound] = MessageType.Proposal,
        [DiscussionPhase.CritiqueRound] = MessageType.Critique,
        [DiscussionPhase.RevisionRound] = MessageType.Revision,
        [DiscussionPhase.Vote] = MessageType.Vote,
        [DiscussionPhase.Synthesis] = MessageType.Synthesis,
    };

    /// <summary>
    /// Validates that the given message type is allowed in the current phase and that the sender
    /// has not already submitted for this round.
    /// </summary>
    /// <returns>A list of validation errors; empty if the submission is valid.</returns>
    public static IReadOnlyList<string> Validate(
        DiscussionPhase currentPhase,
        MessageType messageType,
        DeviceId senderDeviceId,
        IReadOnlySet<DeviceId> alreadySubmitted,
        MemberRole senderRole)
    {
        ArgumentNullException.ThrowIfNull(alreadySubmitted);

        var errors = new List<string>();

        // Observer cannot submit messages
        if (senderRole == MemberRole.Observer)
        {
            errors.Add("Observers cannot submit messages.");
            return errors;
        }

        // Synthesis can only be submitted by the Synthesizer role
        if (messageType == MessageType.Synthesis && senderRole != MemberRole.Synthesizer && senderRole != MemberRole.Owner)
        {
            errors.Add("Only the Synthesizer or Owner can submit a synthesis.");
        }

        // Check phase allows the message type
        if (!AllowedMessageTypes.TryGetValue(currentPhase, out var allowed))
        {
            errors.Add($"No messages are accepted during the '{currentPhase}' phase.");
            return errors;
        }

        if (messageType != allowed)
        {
            errors.Add($"Message type '{messageType}' is not allowed during '{currentPhase}'. Expected '{allowed}'.");
        }

        // Check for duplicate submission
        if (alreadySubmitted.Contains(senderDeviceId))
        {
            errors.Add($"Device '{senderDeviceId}' has already submitted for this round.");
        }

        return errors;
    }

    /// <summary>
    /// Returns the expected message type for a given discussion phase, or null if the phase does not accept messages.
    /// </summary>
    public static MessageType? GetExpectedMessageType(DiscussionPhase phase)
    {
        return AllowedMessageTypes.TryGetValue(phase, out var mt) ? mt : null;
    }
}
