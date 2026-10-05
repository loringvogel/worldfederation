using System.Globalization;
using Federation.Cryptography;
using Federation.Node;
using Federation.Protocol;
using Microsoft.Extensions.Logging;

namespace Federation.Cli;

public static class Program
{
    private const string DefaultConfigPath = "./federation-node.json";

    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var configPath = GetArg(args, "--config") ?? DefaultConfigPath;
        var relayOverride = GetArg(args, "--relay");

        // Strip global flags from args before command parsing
        var commandArgs = StripGlobalFlags(args);

        if (commandArgs.Length == 0)
        {
            // REPL mode
            return await RunReplAsync(configPath, relayOverride).ConfigureAwait(false);
        }

        return await DispatchCommandAsync(commandArgs, configPath, relayOverride).ConfigureAwait(false);
    }

    private static async Task<int> RunReplAsync(string configPath, string? relayOverride)
    {
        Console.WriteLine("Agent Federation Node · type 'help' for commands, 'exit' to quit");
        var config = NodeConfig.Load(configPath);
        if (config.DeviceId is not null)
        {
            Console.WriteLine($"  Device: {config.DeviceName} ({config.DeviceId})");
        }
        else
        {
            Console.WriteLine("  Not registered. Run: register --relay <url> --name <name>");
        }

        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null) break;

            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var replArgs = ParseCommandLine(trimmed);
            // Allow --relay in REPL commands too
            var replRelay = GetArg(replArgs, "--relay") ?? relayOverride;
            var replConfig = GetArg(replArgs, "--config") ?? configPath;
            var stripped = StripGlobalFlags(replArgs);

            try
            {
                await DispatchCommandAsync(stripped, replConfig, replRelay).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // REPL must not crash on any command error
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
#pragma warning restore CA1031

            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<int> DispatchCommandAsync(string[] args, string configPath, string? relayOverride)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "register":
                return await RegisterAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "rooms":
                return await ListRoomsAsync(configPath, relayOverride).ConfigureAwait(false);
            case "create-room":
                return await CreateRoomAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "invite":
                return await InviteAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "discussions":
                return await ListDiscussionsAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "new-discussion":
                return await NewDiscussionAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "use-room":
                return UseRoom(args, configPath);
            case "use-discussion":
                return UseDiscussion(args, configPath);
            case "poll":
                return await PollAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "propose":
                return await SubmitMessageAsync(args, configPath, relayOverride, MessageType.Proposal).ConfigureAwait(false);
            case "critique":
                return await SubmitCritiqueAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "revise":
                return await SubmitMessageAsync(args, configPath, relayOverride, MessageType.Revision).ConfigureAwait(false);
            case "vote":
                return await SubmitVoteAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "synthesize":
                return await SubmitMessageAsync(args, configPath, relayOverride, MessageType.Synthesis).ConfigureAwait(false);
            case "status":
                return PrintStatus(configPath);
            case "emergency-stop":
                return await EmergencyStopAsync(configPath, relayOverride).ConfigureAwait(false);
            case "roster":
                return await RosterAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "provision-client":
                return await ProvisionClientAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "list-public-rooms":
                return await ListPublicRoomsAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "join-room":
                return await JoinRoomAsync(args, configPath, relayOverride).ConfigureAwait(false);
            case "help":
                PrintHelp();
                return 0;
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                Console.Error.WriteLine("Run 'help' for a list of commands.");
                return 1;
        }
    }

    // ───────────────────── register ─────────────────────

    private static async Task<int> RegisterAsync(string[] args, string configPath, string? relayOverride)
    {
        var relay = GetArg(args, "--relay") ?? relayOverride;
        var name = GetArg(args, "--name") ?? "Node";

        if (relay is null)
        {
            Console.Error.WriteLine("Relay URL required. Usage: register --relay <url> --name <name>");
            return 1;
        }

        using var client = new RelayClient(relay);
        if (!await client.HealthCheckAsync().ConfigureAwait(false))
        {
            Console.Error.WriteLine($"Relay unreachable at {relay}");
            return 1;
        }

        var reg = await client.RegisterDeviceAsync(name).ConfigureAwait(false);
        if (reg is null)
        {
            Console.Error.WriteLine("Registration failed.");
            return 1;
        }

        var config = NodeConfig.Load(configPath);
        config.DeviceId = reg.DeviceId;
        config.DeviceToken = reg.DeviceToken;
        config.RelayUrl = relay;
        config.DeviceName = name;
        config.Save(configPath);

        Console.WriteLine($"Registered as {name}");
        Console.WriteLine($"  Device ID : {reg.DeviceId}");
        Console.WriteLine($"  Relay     : {relay}");
        Console.WriteLine($"  Config    : {configPath}");
        Console.WriteLine("Share your Device ID with room owners so they can invite you.");
        return 0;
    }

    // ───────────────────── rooms ─────────────────────

    private static async Task<int> ListRoomsAsync(string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        using var client = CreateClient(config, relayOverride);
        var rooms = await client.ListRoomsAsync().ConfigureAwait(false);

        if (rooms.Count == 0)
        {
            Console.WriteLine("No rooms found.");
            return 0;
        }

        Console.WriteLine($"{"ID",-40} {"Name",-25} {"Members",-8}");
        Console.WriteLine(new string('-', 73));
        foreach (var r in rooms)
        {
            var marker = r.RoomId == config.ActiveRoomId ? " *" : "";
            Console.WriteLine($"{r.RoomId,-40} {r.Name,-25} {r.MemberCount,-8}{marker}");
        }

        if (config.ActiveRoomId is not null)
        {
            Console.WriteLine($"\n* Active room: {config.ActiveRoomId}");
        }

        return 0;
    }

    // ───────────────────── create-room ─────────────────────

    private static async Task<int> CreateRoomAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var name = GetPositionalArg(args, 1);
        if (name is null)
        {
            Console.Error.WriteLine("Usage: create-room <name>");
            return 1;
        }

        var isPublic = HasFlag(args, "--public");
        using var client = CreateClient(config, relayOverride);
        var room = await client.CreateRoomAsync(name, config.DeviceId!, isPublic).ConfigureAwait(false);
        if (room is null) return 1;

        config.Rooms[room.RoomId] = name;
        config.ActiveRoomId = room.RoomId;
        config.Save(configPath);

        Console.WriteLine($"Created room \"{name}\"");
        Console.WriteLine($"  Room ID: {room.RoomId}");
        Console.WriteLine("  Set as active room.");
        return 0;
    }

    // ───────────────────── invite ─────────────────────

    private static async Task<int> InviteAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var targetDeviceId = GetPositionalArg(args, 1);
        if (targetDeviceId is null)
        {
            Console.Error.WriteLine("Usage: invite <deviceId> [--role Participant] [--room <roomId>]");
            return 1;
        }

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;

        var role = GetArg(args, "--role") ?? "Participant";

        using var client = CreateClient(config, relayOverride);
        var membership = await client.InviteDeviceAsync(roomId!, targetDeviceId, role).ConfigureAwait(false);
        if (membership is null) return 1;

        Console.WriteLine($"Invited {targetDeviceId} to room {roomId} as {role}");
        return 0;
    }

    // ───────────────────── discussions ─────────────────────

    private static async Task<int> ListDiscussionsAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;

        using var client = CreateClient(config, relayOverride);
        var discussions = await client.ListDiscussionsAsync(roomId!).ConfigureAwait(false);

        if (discussions.Count == 0)
        {
            Console.WriteLine("No discussions found.");
            return 0;
        }

        Console.WriteLine($"{"ID",-40} {"Topic",-30} {"Phase",-16} {"Round",-6} {"Subs",-5}");
        Console.WriteLine(new string('-', 97));
        foreach (var d in discussions)
        {
            var marker = d.DiscussionId == config.ActiveDiscussionId ? " *" : "";
            Console.WriteLine($"{d.DiscussionId,-40} {Truncate(d.Topic, 30),-30} {d.Phase,-16} {d.CurrentRound,-6} {d.TotalSubmissions,-5}{marker}");
        }

        if (config.ActiveDiscussionId is not null)
        {
            Console.WriteLine($"\n* Active discussion: {config.ActiveDiscussionId}");
        }

        return 0;
    }

    // ───────────────────── new-discussion ─────────────────────

    private static async Task<int> NewDiscussionAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;

        var topic = GetPositionalArg(args, 1);
        if (topic is null)
        {
            Console.Error.WriteLine("Usage: new-discussion <topic> [--room <roomId>]");
            return 1;
        }

        using var client = CreateClient(config, relayOverride);
        var discussion = await client.CreateDiscussionAsync(roomId!, topic, config.DeviceId!).ConfigureAwait(false);
        if (discussion is null) return 1;

        config.ActiveDiscussionId = discussion.DiscussionId;
        config.Save(configPath);

        Console.WriteLine($"Created discussion: {topic}");
        Console.WriteLine($"  Discussion ID: {discussion.DiscussionId}");
        Console.WriteLine($"  Phase: {discussion.Phase}");
        return 0;
    }

    // ───────────────────── use-room ─────────────────────

    private static int UseRoom(string[] args, string configPath)
    {
        var roomId = GetPositionalArg(args, 1);
        if (roomId is null)
        {
            Console.Error.WriteLine("Usage: use-room <roomId>");
            return 1;
        }

        var config = NodeConfig.Load(configPath);
        config.ActiveRoomId = roomId;
        config.Save(configPath);
        Console.WriteLine($"Active room: {roomId}");
        return 0;
    }

    // ───────────────────── use-discussion ─────────────────────

    private static int UseDiscussion(string[] args, string configPath)
    {
        var discussionId = GetPositionalArg(args, 1);
        if (discussionId is null)
        {
            Console.Error.WriteLine("Usage: use-discussion <discussionId>");
            return 1;
        }

        var config = NodeConfig.Load(configPath);
        config.ActiveDiscussionId = discussionId;
        config.Save(configPath);
        Console.WriteLine($"Active discussion: {discussionId}");
        return 0;
    }

    // ───────────────────── poll ─────────────────────

    private static async Task<int> PollAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;
        var discussionId = GetArg(args, "--discussion") ?? config.ActiveDiscussionId;
        if (!RequireDiscussion(discussionId)) return 1;

        var watch = HasFlag(args, "--watch");
        var rid = new RoomId(Guid.Parse(roomId!));
        var did = new DiscussionId(Guid.Parse(discussionId!));

        using var node = BuildNode(config, relayOverride);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        do
        {
            await node.PollAndProcessAsync(rid, did, cts.Token).ConfigureAwait(false);

            var messages = node.Cache.GetMessages(rid, did);
            if (messages.Count > 0)
            {
                Console.WriteLine(new string('\u2500', 45));
                foreach (var msg in messages)
                {
                    Console.WriteLine(
                        $"[{msg.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}] {msg.MessageType.ToString().ToUpperInvariant()} \u00B7 Round {msg.Round} \u00B7 from device {Truncate(msg.SenderDeviceId.Value.ToString(), 8)}");
                    Console.WriteLine($"  {msg.Plaintext}");
                    Console.WriteLine();
                }
                Console.WriteLine(new string('\u2500', 45));
                Console.WriteLine($"{messages.Count} message(s).");
            }
            else
            {
                Console.WriteLine("No new messages.");
            }

            if (watch && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        while (watch && !cts.Token.IsCancellationRequested);

        return 0;
    }

    // ───────────────────── propose / revise / synthesize ─────────────────────

    private static async Task<int> SubmitMessageAsync(string[] args, string configPath, string? relayOverride, MessageType messageType)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;
        var discussionId = GetArg(args, "--discussion") ?? config.ActiveDiscussionId;
        if (!RequireDiscussion(discussionId)) return 1;

        var text = GetPositionalArg(args, 1);
        if (text is null)
        {
            Console.Error.WriteLine($"Usage: {args[0]} <text> [--room <roomId>] [--discussion <discussionId>]");
            return 1;
        }

        // Check discussion state and warn if wrong phase
        using var client = CreateClient(config, relayOverride);
        var state = await client.GetDiscussionStateAsync(roomId!, discussionId!).ConfigureAwait(false);
        if (state is not null)
        {
            var expectedPhase = messageType switch
            {
                MessageType.Proposal => "ProposalRound",
                MessageType.Critique => "CritiqueRound",
                MessageType.Revision => "RevisionRound",
                MessageType.Vote => "Vote",
                MessageType.Synthesis => "Synthesis",
                _ => null,
            };
            if (expectedPhase is not null && !string.Equals(state.Phase, expectedPhase, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Warning: Discussion is in {state.Phase} phase, expected {expectedPhase}.");
            }
        }

        var rid = new RoomId(Guid.Parse(roomId!));
        var did = new DiscussionId(Guid.Parse(discussionId!));
        var round = state?.CurrentRound ?? 1;
        var seq = GetNextSequence(config, discussionId!);

        using var node = BuildNode(config, relayOverride);
        var envelope = node.CreateEnvelope(rid, did, messageType, round, text, seq);
        var ok = await node.SubmitEnvelopeAsync(envelope).ConfigureAwait(false);

        if (!ok)
        {
            Console.Error.WriteLine("Failed to submit envelope.");
            return 1;
        }

        IncrementSequence(config, discussionId!);
        config.Save(configPath);

        Console.WriteLine($"Submitted {messageType} (Message ID: {envelope.MessageId})");
        return 0;
    }

    // ───────────────────── critique ─────────────────────

    private static async Task<int> SubmitCritiqueAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;
        var discussionId = GetArg(args, "--discussion") ?? config.ActiveDiscussionId;
        if (!RequireDiscussion(discussionId)) return 1;

        var targetMessageId = GetPositionalArg(args, 1);
        var text = GetPositionalArg(args, 2);
        if (targetMessageId is null || text is null)
        {
            Console.Error.WriteLine("Usage: critique <messageId> <text> [--room <roomId>] [--discussion <discussionId>]");
            return 1;
        }

        var plaintext = $"TARGET:{targetMessageId}\n{text}";

        using var client = CreateClient(config, relayOverride);
        var state = await client.GetDiscussionStateAsync(roomId!, discussionId!).ConfigureAwait(false);
        if (state is not null && !string.Equals(state.Phase, "CritiqueRound", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Warning: Discussion is in {state.Phase} phase, expected CritiqueRound.");
        }

        var rid = new RoomId(Guid.Parse(roomId!));
        var did = new DiscussionId(Guid.Parse(discussionId!));
        var round = state?.CurrentRound ?? 1;
        var seq = GetNextSequence(config, discussionId!);

        using var node = BuildNode(config, relayOverride);
        var envelope = node.CreateEnvelope(rid, did, MessageType.Critique, round, plaintext, seq);
        var ok = await node.SubmitEnvelopeAsync(envelope).ConfigureAwait(false);

        if (!ok)
        {
            Console.Error.WriteLine("Failed to submit envelope.");
            return 1;
        }

        IncrementSequence(config, discussionId!);
        config.Save(configPath);

        Console.WriteLine($"Submitted critique (Message ID: {envelope.MessageId})");
        return 0;
    }

    // ───────────────────── vote ─────────────────────

    private static async Task<int> SubmitVoteAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;
        var discussionId = GetArg(args, "--discussion") ?? config.ActiveDiscussionId;
        if (!RequireDiscussion(discussionId)) return 1;

        var choiceStr = GetPositionalArg(args, 1);
        if (choiceStr is null || !Enum.TryParse<VoteChoice>(choiceStr, ignoreCase: true, out var choice))
        {
            Console.Error.WriteLine("Usage: vote <approve|reject|abstain> [rationale]");
            return 1;
        }

        var rationale = GetPositionalArg(args, 2) ?? "";
        var plaintext = $"VOTE:{choice}\nRATIONALE:{rationale}";

        using var client = CreateClient(config, relayOverride);
        var state = await client.GetDiscussionStateAsync(roomId!, discussionId!).ConfigureAwait(false);
        if (state is not null && !string.Equals(state.Phase, "Vote", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Warning: Discussion is in {state.Phase} phase, expected Vote.");
        }

        var rid = new RoomId(Guid.Parse(roomId!));
        var did = new DiscussionId(Guid.Parse(discussionId!));
        var round = state?.CurrentRound ?? 1;
        var seq = GetNextSequence(config, discussionId!);

        using var node = BuildNode(config, relayOverride);
        var envelope = node.CreateEnvelope(rid, did, MessageType.Vote, round, plaintext, seq);
        var ok = await node.SubmitEnvelopeAsync(envelope).ConfigureAwait(false);

        if (!ok)
        {
            Console.Error.WriteLine("Failed to submit envelope.");
            return 1;
        }

        IncrementSequence(config, discussionId!);
        config.Save(configPath);

        Console.WriteLine($"Submitted vote: {choice} (Message ID: {envelope.MessageId})");
        return 0;
    }

    // ───────────────────── status ─────────────────────

    private static int PrintStatus(string configPath)
    {
        var config = NodeConfig.Load(configPath);

        Console.WriteLine("Federation Node Status");
        Console.WriteLine(new string('\u2500', 22));

        if (config.DeviceId is not null)
        {
            Console.WriteLine($"Device    : {config.DeviceName} (registered)");
            Console.WriteLine($"Device ID : {config.DeviceId}");
        }
        else
        {
            Console.WriteLine("Device    : (not registered)");
        }

        Console.WriteLine($"Relay     : {config.RelayUrl}");
        Console.WriteLine($"Config    : {configPath}");
        Console.WriteLine();

        if (config.ActiveRoomId is not null)
        {
            var roomName = config.Rooms.TryGetValue(config.ActiveRoomId, out var n) ? n : "(unknown)";
            Console.WriteLine($"Active Room       : {roomName} ({config.ActiveRoomId})");
        }
        else
        {
            Console.WriteLine("Active Room       : (none)");
        }

        if (config.ActiveDiscussionId is not null)
        {
            Console.WriteLine($"Active Discussion : {config.ActiveDiscussionId}");
        }
        else
        {
            Console.WriteLine("Active Discussion : (none)");
        }

        if (config.Rooms.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Known Rooms ({config.Rooms.Count}):");
            foreach (var (id, name) in config.Rooms)
            {
                var marker = id == config.ActiveRoomId ? "* " : "  ";
                Console.WriteLine($"  {marker}{id} {name}");
            }
        }

        return 0;
    }

    // ───────────────────── emergency-stop ─────────────────────

    private static async Task<int> EmergencyStopAsync(string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        Console.Write("This will wipe all local keys and revoke your device from all rooms. Type CONFIRM to proceed: ");
        var confirmation = Console.ReadLine();
        if (!string.Equals(confirmation?.Trim(), "CONFIRM", StringComparison.Ordinal))
        {
            Console.WriteLine("Aborted.");
            return 1;
        }

        using var client = CreateClient(config, relayOverride);
        await client.EmergencyRevokeAsync(config.DeviceId!).ConfigureAwait(false);

        config.DeviceId = null;
        config.DeviceToken = null;
        config.ActiveRoomId = null;
        config.ActiveDiscussionId = null;
        config.Rooms.Clear();
        config.SenderSequences.Clear();
        config.Save(configPath);

        Console.WriteLine("Emergency stop complete. Device revoked. Config cleared.");
        return 0;
    }

    // ───────────────────── roster ─────────────────────

    private static async Task<int> RosterAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetArg(args, "--room") ?? config.ActiveRoomId;
        if (!RequireRoom(roomId)) return 1;

        using var client = CreateClient(config, relayOverride);
        var members = await client.GetMembersAsync(roomId!).ConfigureAwait(false);

        if (members.Count == 0)
        {
            Console.WriteLine("No members found.");
            return 0;
        }

        Console.WriteLine($"{"Name",-20} {"Role",-14} {"Device ID",-38} {"Joined",-20}");
        Console.WriteLine(new string('─', 92));
        foreach (var m in members)
        {
            var you = m.DeviceId == config.DeviceId ? " ◀ you" : "";
            Console.WriteLine(
                $"{m.DisplayName,-20} {m.Role,-14} {m.DeviceId,-38} {m.JoinedAt.ToLocalTime():yyyy-MM-dd HH:mm}{you}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: address a specific agent with @Name: in your message.");
        Console.WriteLine("  Example: propose \"@MacDev: please run the iOS build and report results\"");
        return 0;
    }

    // ───────────────────── provision-client ─────────────────────

    private static async Task<int> ProvisionClientAsync(string[] args, string configPath, string? relayOverride)
    {
        var relay = GetArg(args, "--relay") ?? relayOverride;
        var name  = GetArg(args, "--name");
        var mcpUrl = GetArg(args, "--mcp-url");

        if (relay is null)
        {
            Console.Error.WriteLine("Usage: provision-client --name <name> --relay <url> [--mcp-url <url>]");
            Console.Error.WriteLine("  --relay    Relay URL (e.g. https://relay.example.com)");
            Console.Error.WriteLine("  --name     Client display name (e.g. Alice)");
            Console.Error.WriteLine("  --mcp-url  Public URL of federation-remote-mcp (default: relay host on port 5002)");
            return 1;
        }

        name ??= "Client";

        // Derive default MCP URL from relay URL (same host, port 5002)
        if (mcpUrl is null)
        {
            try
            {
                var relayUri = new Uri(relay.TrimEnd('/'));
                mcpUrl = $"{relayUri.Scheme}://{relayUri.Host}:5002";
            }
            catch (UriFormatException)
            {
                Console.Error.WriteLine("Could not derive MCP URL from relay URL. Pass --mcp-url explicitly.");
                return 1;
            }
        }

        using var client = new RelayClient(relay);
        if (!await client.HealthCheckAsync().ConfigureAwait(false))
        {
            Console.Error.WriteLine($"Relay unreachable at {relay}");
            return 1;
        }

        var reg = await client.RegisterDeviceAsync(name).ConfigureAwait(false);
        if (reg is null)
        {
            Console.Error.WriteLine("Registration failed.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"Client \"{name}\" provisioned successfully.");
        Console.WriteLine();
        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine("  Paste the following into claude.ai → Settings → Integrations");
        Console.WriteLine("  → Add MCP Server:");
        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine($"  MCP Server URL : {mcpUrl.TrimEnd('/')}/mcp");
        Console.WriteLine($"  X-Device-Id    : {reg.DeviceId}");
        Console.WriteLine($"  X-Device-Token : {reg.DeviceToken}");
        Console.WriteLine();
        Console.WriteLine("──────────────────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine($"  Device ID (share this with room owners to invite {name}):");
        Console.WriteLine($"  {reg.DeviceId}");
        Console.WriteLine();
        Console.WriteLine($"  To invite {name} to a room, run:");
        Console.WriteLine($"    federation invite {reg.DeviceId} --role Participant");
        Console.WriteLine();

        return 0;
    }

    // ───────────────────── list-public-rooms ─────────────────────

    private static async Task<int> ListPublicRoomsAsync(string[] args, string configPath, string? relayOverride)
    {
        var relay = GetArg(args, "--relay") ?? relayOverride;
        if (relay is null)
        {
            var config = NodeConfig.Load(configPath);
            relay = config.RelayUrl;
        }

        if (relay is null)
        {
            Console.Error.WriteLine("Relay URL required. Use --relay <url> or register first.");
            return 1;
        }

        using var client = new RelayClient(relay);
        var rooms = await client.ListPublicRoomsAsync().ConfigureAwait(false);

        if (rooms.Count == 0)
        {
            Console.WriteLine("No public rooms found.");
            return 0;
        }

        Console.WriteLine($"{"ID",-40} {"Name",-25} {"Members",-8}");
        Console.WriteLine(new string('-', 73));
        foreach (var r in rooms)
        {
            Console.WriteLine($"{r.RoomId,-40} {r.Name,-25} {r.MemberCount,-8}");
        }

        return 0;
    }

    // ───────────────────── join-room ─────────────────────

    private static async Task<int> JoinRoomAsync(string[] args, string configPath, string? relayOverride)
    {
        var config = NodeConfig.Load(configPath);
        if (!RequireRegistered(config)) return 1;

        var roomId = GetPositionalArg(args, 1);
        if (roomId is null)
        {
            Console.Error.WriteLine("Usage: join-room <roomId> [--relay <url>] [--set-active]");
            return 1;
        }

        using var client = CreateClient(config, relayOverride);
        var ok = await client.JoinPublicRoomAsync(roomId).ConfigureAwait(false);
        if (!ok) return 1;

        Console.WriteLine($"Joined room {roomId}.");

        var setActive = HasFlag(args, "--set-active") || config.ActiveRoomId is null;
        if (setActive)
        {
            config.ActiveRoomId = roomId;
            if (!config.Rooms.ContainsKey(roomId))
                config.Rooms[roomId] = roomId;
            config.Save(configPath);
            Console.WriteLine("  Set as active room.");
        }

        return 0;
    }

    // ───────────────────── help ─────────────────────

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Agent Federation CLI
            ====================

            Commands:
              register --relay <url> --name <name>   Register this node with a relay
              rooms                                   List rooms you belong to
              create-room <name> [--public]            Create a new council room (--public for global)
              invite <deviceId> [--role Role]         Invite a device to the active room
              discussions                             List discussions in the active room
              new-discussion <topic>                  Create a new discussion
              use-room <roomId>                       Set the active room
              use-discussion <discussionId>           Set the active discussion
              poll [--watch]                          Fetch new messages
              propose <text>                          Submit a proposal
              critique <messageId> <text>             Submit a critique
              revise <text>                           Submit a revision
              vote <approve|reject|abstain> [reason]  Submit a vote
              synthesize <text>                       Submit a synthesis
              status                                  Show node status
              emergency-stop                          Revoke device and wipe keys
              roster [--room <roomId>]                List named agents in the active room
              provision-client --name <n> --relay <u> Register a claude.ai client and print MCP settings
              list-public-rooms [--relay <url>]       List public rooms on a relay
              join-room <roomId> [--set-active]       Join a public room
              help                                    Show this help

            Global flags:
              --config <path>    Config file (default: ./federation-node.json)
              --relay <url>      Override relay URL

            Run without a command to enter interactive REPL mode.
            """);
    }

    // ───────────────────── helpers ─────────────────────

    private static CouncilNode BuildNode(NodeConfig config, string? relayOverride)
    {
        var relayUrl = relayOverride ?? config.RelayUrl;
        var crypto = new InMemoryCryptoProvider();
        var (publicKey, privateKey) = crypto.GenerateDeviceKeyPair();
        var http = new HttpClient();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<CouncilNode>();
        var options = new CouncilNodeOptions
        {
            RelayBaseUrl = relayUrl,
            DeviceId = new DeviceId(Guid.Parse(config.DeviceId!)),
            DeviceToken = config.DeviceToken!,
            RoomIds = config.Rooms.Keys.Select(k => new RoomId(Guid.Parse(k))).ToArray(),
            PollInterval = TimeSpan.FromSeconds(5),
            RelayUrls = [relayUrl],
        };
        // Phase 1: derive a deterministic group key from the room ID so all nodes in the
        // same room can decrypt each other's messages without a real key exchange.
        // NOT secure — replace with MLS key exchange in Phase 2.
        var groupSession = new InMemoryGroupSession();
        var keyRoomId = config.ActiveRoomId is not null
            ? Guid.Parse(config.ActiveRoomId)
            : (options.RoomIds.Length > 0 ? options.RoomIds[0].Value : (Guid?)null);
        if (keyRoomId.HasValue)
        {
            var roomKey = System.Security.Cryptography.SHA256.HashData(keyRoomId.Value.ToByteArray());
            groupSession.SetGroupKey(roomKey, epoch: 1);
        }
        return new CouncilNode(options, crypto, groupSession, new InMemoryKeyStore(),
            new InMemoryLocalCache(), http, logger, publicKey, privateKey);
    }

    private static RelayClient CreateClient(NodeConfig config, string? relayOverride)
    {
        var relay = relayOverride ?? config.RelayUrl;
        return new RelayClient(relay, config.DeviceId, config.DeviceToken);
    }

    private static bool RequireRegistered(NodeConfig config)
    {
        if (config.DeviceId is not null) return true;
        Console.Error.WriteLine("Not registered. Run: register --relay <url> --name <name>");
        return false;
    }

    private static bool RequireRoom(string? roomId)
    {
        if (roomId is not null) return true;
        Console.Error.WriteLine("No active room. Run: rooms, then: use-room <id>");
        return false;
    }

    private static bool RequireDiscussion(string? discussionId)
    {
        if (discussionId is not null) return true;
        Console.Error.WriteLine("No active discussion. Run: discussions, then: use-discussion <id>");
        return false;
    }

    private static long GetNextSequence(NodeConfig config, string discussionId)
    {
        config.SenderSequences.TryGetValue(discussionId, out var seq);
        return seq;
    }

    private static void IncrementSequence(NodeConfig config, string discussionId)
    {
        config.SenderSequences.TryGetValue(discussionId, out var seq);
        config.SenderSequences[discussionId] = seq + 1;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static string? GetPositionalArg(string[] args, int index)
    {
        // Skip the command name (index 0) and any --flag pairs
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                i++; // skip the value too
                continue;
            }
            positional++;
            if (positional == index)
            {
                return args[i];
            }
        }
        return null;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] StripGlobalFlags(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "--relay", StringComparison.OrdinalIgnoreCase))
            {
                i++; // skip value
                continue;
            }
            result.Add(args[i]);
        }
        return result.ToArray();
    }

    private static string[] ParseCommandLine(string line)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString());
        }

        return args.ToArray();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}
