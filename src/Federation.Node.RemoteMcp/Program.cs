// Federation Remote MCP Server
//
// Exposes the 8 federation council tools over HTTP/SSE so that
// claude.ai Pro users can connect their Claude instance to a shared
// deliberation room without installing anything locally.
//
// ── How it works ──────────────────────────────────────────────────────────
//
//   1. Developer runs:
//        federation provision-client --name "Alice" --relay https://relay.example.com
//      → gets back device ID + token for Alice
//
//   2. Developer gives Alice:
//        MCP server URL : https://relay.example.com/mcp/sse
//        Header         : X-Device-Id:    <alice-device-id>
//        Header         : X-Device-Token: <alice-device-token>
//
//   3. Alice adds those to claude.ai → Settings → Integrations → MCP Servers
//
//   4. Alice's Claude now has the 8 federation tools:
//        list_rooms, get_discussion, submit_proposal, submit_critique,
//        submit_revision, submit_vote, submit_synthesis, acknowledge
//
//   5. Developer's Claude Code also has these tools via the local stdio MCP.
//      Both are connected to the same relay room → they communicate directly.
//
// ── Hosting ───────────────────────────────────────────────────────────────
//
//   Development (ngrok):
//     1. dotnet run --project src/Federation.Node.RemoteMcp -- --urls http://localhost:5002
//     2. ngrok http 5002
//     3. Use the ngrok HTTPS URL as the MCP server URL
//
//   Production (Docker):
//     Included in docker-compose.yml alongside the relay.
//     Set FEDERATION_RELAY_URL env var to the relay's internal URL.
//
// ── Env vars ──────────────────────────────────────────────────────────────
//   FEDERATION_RELAY_URL   Default relay URL (clients may override via header)
//   ASPNETCORE_URLS        Listening address (default: http://+:5002)

using Federation.Node.RemoteMcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer(opts =>
    {
        opts.ServerInfo = new() { Name = "federation", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

builder.Logging.AddConsole();

// Bind on 5002 by default so it doesn't conflict with the relay (5000) or CLI MCP (5001)
builder.WebHost.UseUrls(
    builder.Configuration["urls"] ?? "http://+:5002");

var app = builder.Build();

// Health endpoint so provision-client can verify reachability
app.MapGet("/health", () => Results.Ok(new { status = "ok", server = "federation-remote-mcp" }));

app.MapMcp("/mcp");

app.Run();
