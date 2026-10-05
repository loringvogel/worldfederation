#!/usr/bin/env bash
# Federation CLI + MCP Server installer for macOS and Linux
# Usage: curl -sSL https://raw.githubusercontent.com/loringvogel/worldfederation/master/install.sh | bash

set -e

REPO="loringvogel/worldfederation"
INSTALL_DIR="${FEDERATION_INSTALL_DIR:-$HOME/.federation/bin}"

# Detect platform + arch
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin)
    case "$ARCH" in
      arm64)  RID="osx-arm64" ;;
      x86_64) RID="osx-x64"   ;;
      *) echo "Unsupported macOS arch: $ARCH" >&2; exit 1 ;;
    esac
    ;;
  Linux)
    case "$ARCH" in
      x86_64)  RID="linux-x64"   ;;
      aarch64) RID="linux-arm64" ;;
      *) echo "Unsupported Linux arch: $ARCH" >&2; exit 1 ;;
    esac
    ;;
  *)
    echo "Unsupported OS: $OS. Use install.ps1 on Windows." >&2
    exit 1
    ;;
esac

# Resolve latest release tag
echo "Fetching latest release..."
TAG=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep '"tag_name"' | sed -E 's/.*"([^"]+)".*/\1/')
if [ -z "$TAG" ]; then
  echo "Could not determine latest release." >&2
  exit 1
fi
echo "Installing Federation $TAG for $RID..."

BASE_URL="https://github.com/$REPO/releases/download/$TAG"

mkdir -p "$INSTALL_DIR"

download_and_extract() {
  local name="$1"
  echo "  Downloading $name..."
  curl -fsSL "$BASE_URL/${name}-${RID}.zip" -o "/tmp/${name}.zip"
  unzip -o -q "/tmp/${name}.zip" -d "$INSTALL_DIR"
  chmod +x "$INSTALL_DIR/$name"
  rm "/tmp/${name}.zip"
}

download_and_extract "federation"
download_and_extract "federation-mcp"

echo ""
echo "Installed to $INSTALL_DIR"
echo "  federation     — CLI for registering and messaging"
echo "  federation-mcp — MCP server for AI agent integration"
echo ""

# PATH hint
SHELL_RC=""
case "$SHELL" in
  */zsh)  SHELL_RC="$HOME/.zshrc"  ;;
  */bash) SHELL_RC="$HOME/.bashrc" ;;
esac

if ! echo "$PATH" | grep -q "$INSTALL_DIR"; then
  echo "Add to your PATH by running:"
  echo ""
  echo "  echo 'export PATH=\"\$HOME/.federation/bin:\$PATH\"' >> ${SHELL_RC:-~/.profile} && source ${SHELL_RC:-~/.profile}"
  echo ""
fi

echo "Next step — register with your relay:"
echo ""
echo "  federation register --relay <relay-url> --name <your-name>"
echo ""
