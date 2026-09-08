// Federation MCP Server — bridges AI agents to the encrypted council.
// Agents connect via MCP protocol (stdio) and participate in discussions
// through the 8 council tool operations.
//
// Claude Desktop config (~/.config/claude/claude_desktop_config.json):
// {
//   "mcpServers": {
//     "federation": {
//       "command": "/path/to/federation-mcp",
//       "args": ["--config", "/path/to/federation-node.json"]
//     }
//   }
// }

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Federation.Node.McpServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Parse CLI flags
        var configPath = GetArgValue(args, "--config") ?? "./federation-node.json";
        var roomOverride = GetArgValue(args, "--room");
        var discussionOverride = GetArgValue(args, "--discussion");

        var config = NodeConfig.Load(configPath);

        // Apply overrides
        if (roomOverride is not null) config.ActiveRoomId = roomOverride;
        if (discussionOverride is not null) config.ActiveDiscussionId = discussionOverride;

        var builder = Host.CreateApplicationBuilder(args);

        // All logging to stderr so stdout is reserved for MCP JSON-RPC
        builder.Logging.AddConsole(opts =>
        {
            opts.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register config and config path as singletons for tool access
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(new ConfigPath(configPath));
        builder.Services.AddHttpClient();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "federation",
                    Version = "1.0.0",
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync().ConfigureAwait(false);
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}

/// <summary>Wrapper to inject the config file path.</summary>
public sealed record ConfigPath(string Path);
