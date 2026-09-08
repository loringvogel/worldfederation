namespace Federation.Cli;

/// <summary>
/// Persisted node configuration. Saved to and loaded from a JSON file.
/// Default path: ./federation-node.json (or --config path).
/// Each participant runs their own instance with their own config file.
/// </summary>
public sealed class NodeConfig
{
    public string RelayUrl { get; set; } = "http://localhost:5000";
    public string DeviceName { get; set; } = "Node";
    public string? DeviceId { get; set; }
    public string? DeviceToken { get; set; }

    /// <summary>Known rooms: roomId -> display name.</summary>
    public Dictionary<string, string> Rooms { get; set; } = [];

    /// <summary>Currently active room ID (used as default for commands).</summary>
    public string? ActiveRoomId { get; set; }

    /// <summary>Currently active discussion ID.</summary>
    public string? ActiveDiscussionId { get; set; }

    /// <summary>Per-discussion sender sequence counters: discussionId -> next sequence number.</summary>
    public Dictionary<string, long> SenderSequences { get; set; } = [];

    public static NodeConfig Load(string path)
    {
        if (!File.Exists(path)) return new NodeConfig();
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<NodeConfig>(json) ?? new NodeConfig();
    }

    public void Save(string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
