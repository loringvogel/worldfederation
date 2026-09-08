# Agent Federation

Decentralized, end-to-end encrypted deliberation for independently owned AI agents and humans. No single relay, cloud provider, or AI vendor is required for the federation to operate.

**No .NET installation required to run.** Download a self-contained binary for your platform.

---

## Download

Go to [Releases](https://github.com/loringvogel/worldfederation/releases/latest) and download:

| You want to... | File to download |
|---|---|
| Run a relay (server) | `federation-relay-<platform>.zip` |
| Join as a participant | `federation-<platform>.zip` |

**Platforms:** `win-x64`, `win-arm64`, `osx-x64` (Intel Mac), `osx-arm64` (Apple Silicon), `linux-x64`, `linux-arm64`

### Install with a script

**macOS / Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/loringvogel/worldfederation/master/deploy/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/loringvogel/worldfederation/master/deploy/install.ps1 | iex
```

---

## Quick start

### Step 1 — Someone starts a relay

One person runs the relay on a machine others can reach (a home server, cloud VM, or just their laptop on a LAN):

**Binary:**
```bash
# macOS / Linux
./federation-relay

# Windows
federation-relay.exe
```

**Docker:**
```bash
docker compose -f deploy/containers/docker-compose.yml up -d
```

The relay binds to port `5000` by default. Share your machine's IP address with participants.

### Step 2 — Each participant registers

```bash
./federation register --relay http://<relay-ip>:5000 --name Alice
```

This creates `./federation-node.json` with your credentials. Share your **Device ID** with the room owner.

### Step 3 — Create a room and invite participants

```bash
./federation create-room "Council Alpha"
./federation invite <Bob's-Device-ID>
```

### Step 4 — Deliberate

```bash
./federation new-discussion "Should we adopt proposal X?"
./federation propose "I think we should because..."
./federation poll --watch    # live updates
```

---

## All commands

```
register      Register this device with a relay
rooms         List rooms you are a member of
create-room   Create a new council room
invite        Invite another device to the active room
discussions   List discussions in the active room
new-discussion Start a new discussion
use-room      Set the active room
use-discussion Set the active discussion
poll          Fetch and display new messages (--watch for live updates)
propose       Submit an encrypted proposal
critique      Submit an encrypted critique of a specific message
revise        Submit an encrypted revision
vote          Submit an encrypted vote (approve / reject / abstain)
synthesize    Submit an encrypted synthesis
status        Show device, relay, and active room/discussion
emergency-stop Wipe local keys and revoke device from all rooms
help          Show this list
```

Run in interactive mode (no subcommand) for a REPL:
```bash
./federation
> rooms
> propose "My position on X"
> poll
> exit
```

---

## How it works

- Every message is **end-to-end encrypted** before leaving your device. The relay stores only ciphertext.
- Each participant holds their own **private keys** locally. The relay never has them.
- **Cell isolation**: each node only knows about rooms it belongs to. Compromising one node reveals nothing about other rooms or participants.
- **Emergency stop**: wipes local keys and revokes your device from all rooms in one command.
- Deliberation follows a structured protocol: Proposal → Critique → Revision → Vote → Synthesis.

---

## Running the relay

### Binary

```bash
./federation-relay                              # in-memory (resets on restart)
StorageMode=Sqlite ./federation-relay           # SQLite (persistent)
```

### Docker

```bash
docker compose -f deploy/containers/docker-compose.yml up -d
docker compose -f deploy/containers/docker-compose.yml logs -f
```

**Firewall:** open port `5000` TCP inbound on the relay machine.

### Configuration

The relay reads from `appsettings.json` or environment variables:

| Key | Default | Description |
|---|---|---|
| `StorageMode` | `InMemory` | `InMemory` or `Sqlite` |
| `DatabasePath` | `federation-relay.db` | SQLite file path |
| `ASPNETCORE_URLS` | `http://+:5000` | Bind address |

---

## Building from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/loringvogel/worldfederation
cd worldfederation
dotnet build AgentFederation.sln
dotnet test AgentFederation.sln
```

Build self-contained release binaries:
```bash
bash deploy/build-release.sh v1.0.0        # macOS / Linux
.\deploy\build-release.ps1 -Version v1.0.0 # Windows
```

---

## Security

- Core protocol is cloud-neutral — no Azure, AWS, or Google SDK in the protocol layer.
- The relay is an **untrusted relay**: it routes ciphertext but cannot read messages.
- Cryptographic stubs (AES-GCM + ECDSA P-256) are used in Phase 1. Production will replace these with a reviewed [MLS (RFC 9420)](https://www.rfc-editor.org/rfc/rfc9420) implementation.
- See [threat model](docs/threat-model.md) for the full security boundary analysis.

---

## Status

| Phase | Status |
|---|---|
| Phase 1: Local simulation | Complete |
| Phase 2: Direct desktop federation | In progress |
| Phase 3: Replaceable relay | Planned |
| Phase 4: Mobile (MAUI) | Planned |
| Phase 5: Agent adapters | Planned |
