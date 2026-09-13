#!/usr/bin/env bash
# Agent Federation — macOS installer
#
# Builds and installs:
#   - The MAUI desktop app (.app bundle) → /Applications/Agent Federation.app
#   - The relay server (self-contained)   → ~/Library/Application Support/AgentFederation/relay/
#
# Requirements:
#   - .NET 9 SDK: https://dotnet.microsoft.com/download
#   - MAUI workload: dotnet workload install maui
#   - Xcode Command Line Tools (for Mac Catalyst builds): xcode-select --install
#   - On Apple Silicon, run: sudo dotnet workload install maccatalyst
#
# Usage:
#   chmod +x install-mac.sh
#   ./install-mac.sh

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUPPORT_DIR="$HOME/Library/Application Support/AgentFederation"
APP_DEST="/Applications/Agent Federation.app"
RELAY_DEST="$SUPPORT_DIR/relay"
LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
RELAY_PLIST="$LAUNCH_AGENTS_DIR/com.worldfederation.relay.plist"

echo ""
echo "======================================"
echo "  Agent Federation — macOS Installer"
echo "======================================"
echo ""

# ── Detect architecture ────────────────────────────────────────────────────

ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
    TFM_SUFFIX="maccatalyst-arm64"
else
    RID="osx-x64"
    TFM_SUFFIX="maccatalyst-x64"
fi
echo "Architecture: $ARCH ($RID)"
echo ""

# ── Build relay ────────────────────────────────────────────────────────────

echo "Building relay server..."
mkdir -p "$RELAY_DEST"
dotnet publish "$REPO_DIR/src/Federation.Relay/Federation.Relay.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained \
    -o "$RELAY_DEST"
echo "  Relay → $RELAY_DEST"

# ── Build MAUI app ─────────────────────────────────────────────────────────

echo ""
echo "Building MAUI desktop app (Mac Catalyst)..."
PUBLISH_OUT="$SUPPORT_DIR/app-publish"
mkdir -p "$PUBLISH_OUT"

dotnet publish "$REPO_DIR/src/Federation.App.Maui/Federation.App.Maui.csproj" \
    -f net9.0-maccatalyst \
    -c Release \
    --self-contained \
    -o "$PUBLISH_OUT"

# The publish output contains a .app bundle — move it to /Applications
APP_BUNDLE=$(find "$PUBLISH_OUT" -name "*.app" -maxdepth 2 | head -n 1)
if [ -z "$APP_BUNDLE" ]; then
    echo "ERROR: No .app bundle found in $PUBLISH_OUT"
    exit 1
fi

echo "  Found bundle: $APP_BUNDLE"

if [ -d "$APP_DEST" ]; then
    echo "  Removing existing: $APP_DEST"
    rm -rf "$APP_DEST"
fi
cp -R "$APP_BUNDLE" "$APP_DEST"
echo "  Installed: $APP_DEST"

# ── LaunchAgent plist for the relay ───────────────────────────────────────

mkdir -p "$LAUNCH_AGENTS_DIR"
RELAY_BINARY="$RELAY_DEST/federation-relay"

cat > "$RELAY_PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.worldfederation.relay</string>
    <key>ProgramArguments</key>
    <array>
        <string>${RELAY_BINARY}</string>
        <string>--urls</string>
        <string>http://localhost:5000</string>
    </array>
    <key>RunAtLoad</key>
    <false/>
    <key>KeepAlive</key>
    <false/>
    <key>StandardOutPath</key>
    <string>${HOME}/Library/Logs/AgentFederation/relay.log</string>
    <key>StandardErrorPath</key>
    <string>${HOME}/Library/Logs/AgentFederation/relay-error.log</string>
</dict>
</plist>
EOF

mkdir -p "$HOME/Library/Logs/AgentFederation"
echo "  LaunchAgent plist → $RELAY_PLIST"

# ── Done ───────────────────────────────────────────────────────────────────

echo ""
echo "======================================"
echo "  Installation complete!"
echo "======================================"
echo ""
echo "  App: $APP_DEST"
echo "  Relay: $RELAY_DEST"
echo ""
echo "  To use:"
echo "    1. Start the relay:"
echo "         launchctl load '$RELAY_PLIST'"
echo "       Or run manually: '$RELAY_BINARY' --urls http://localhost:5000"
echo ""
echo "    2. Open: Agent Federation.app  (from /Applications or Spotlight)"
echo ""
echo "    3. Go to the Setup tab:"
echo "         - Set Relay URL to http://localhost:5000"
echo "         - Register your device"
echo "         - Choose your AI provider (Ollama, OpenAI, etc.)"
echo ""
echo "  To stop the relay:"
echo "    launchctl unload '$RELAY_PLIST'"
echo ""
