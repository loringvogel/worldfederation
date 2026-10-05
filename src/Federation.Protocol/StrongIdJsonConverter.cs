using System.Text.Json;
using System.Text.Json.Serialization;

namespace Federation.Protocol;

/// <summary>
/// Serializes Guid-based strong IDs as plain UUID strings rather than {"value":"..."} objects.
/// Usage: [JsonConverter(typeof(StrongIdJsonConverter&lt;DeviceId&gt;))]
/// </summary>
public sealed class StrongIdJsonConverter<T> : JsonConverter<T> where T : struct
{
    private static readonly System.Reflection.ConstructorInfo? _ctor =
        typeof(T).GetConstructor([typeof(Guid)]);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var guid = reader.GetGuid();
        return _ctor is not null
            ? (T)_ctor.Invoke([guid])
            : default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var prop = typeof(T).GetProperty("Value");
        if (prop?.GetValue(value) is Guid g)
            writer.WriteStringValue(g);
        else
            writer.WriteStringValue(value.ToString());
    }
}

/// <summary>Serializes EpochId (long-based) as a plain number.</summary>
public sealed class EpochIdJsonConverter : JsonConverter<EpochId>
{
    public override EpochId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, EpochId value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}
