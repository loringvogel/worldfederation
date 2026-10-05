using System.Text.Json.Serialization;

namespace Federation.Protocol;

/// <summary>Strongly typed identifier for a council room.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<RoomId>))]
public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a discussion within a room.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<DiscussionId>))]
public readonly record struct DiscussionId(Guid Value)
{
    public static DiscussionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a single round within a discussion.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<RoundId>))]
public readonly record struct RoundId(Guid Value)
{
    public static RoundId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for an encrypted message envelope.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<MessageId>))]
public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a device (agent or human endpoint).</summary>
[JsonConverter(typeof(StrongIdJsonConverter<DeviceId>))]
public readonly record struct DeviceId(Guid Value)
{
    public static DeviceId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a human operator.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<HumanId>))]
public readonly record struct HumanId(Guid Value)
{
    public static HumanId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a room membership.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<MembershipId>))]
public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for an invitation to join a room.</summary>
[JsonConverter(typeof(StrongIdJsonConverter<InvitationId>))]
public readonly record struct InvitationId(Guid Value)
{
    public static InvitationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly typed identifier for a cryptographic epoch (key generation).</summary>
[JsonConverter(typeof(EpochIdJsonConverter))]
public readonly record struct EpochId(long Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
