#!/usr/bin/env bash
# Agent Federation CLI installer for macOS and Linux
# Usage: curl -fsSL https://raw.githubusercontent.com/loringvogel/worldfederation/master/deploy/install.sh | bash

set -euo pipefail

REPO="loringvogel/worldfederation"
INSTALL_DIR="${FEDERATION_INSTALL_DIR:-$HOME/.local/bin}"

echo "Agent Federation Installer"
echo "──────────────────────────"

# Detect OS and architecture
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin)
    case "$ARCH" in
      arm64)  RID="osx-arm64" ;;
      x86_64) RID="osx-x64"   ;;
      *)      echo "Unsupported macOS architecture: $ARCH" >&2; exit 1 ;;
    esac
    ;;
  Linux)
    case "$ARCH" in
      aarch64|arm64) RID="linux-arm64" ;;
      x86_64)        RID="linux-x64"   ;;
      *)             echo "Unsupported Linux architecture: $ARCH" >&2; exit 1 ;;
    esac
    ;;
  *)
    echo "Unsupported OS: $OS" >&2
    exit 1
    ;;
esac

# Resolve latest version
VERSION=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
  | grep '"tag_name"' | head -1 | cut -d'"' -f4)

echo "Version : $VERSION"
echo "Platform: $RID"
echo "Install : $INSTALL_DIR"
echo ""

# Download and install CLI
ZIP_NAME="federation-${RID}.zip"
URL="https://github.com/$REPO/releases/download/$VERSION/$ZIP_NAME"
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

echo -n "Downloading $ZIP_NAME... "
curl -fsSL "$URL" -o "$TMP/$ZIP_NAME"
echo "done"

mkdir -p "$INSTALL_DIR"
unzip -qo "$TMP/$ZIP_NAME" -d "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/federation"

# Suggest PATH addition if needed
if ! echo "$PATH" | grep -q "$INSTALL_DIR"; then
  echo ""
  echo "Add to your shell profile (.bashrc / .zshrc):"
  echo "  export PATH=\"\$PATH:$INSTALL_DIR\""
fi

echo ""
echo "Installed! Try:"
echo "  federation help"
echo "  federation register --relay http://<relay-ip>:5000 --name YourName"
