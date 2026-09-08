#!/usr/bin/env bash
# Builds self-contained release binaries for the current platform.
# Output goes to ./release/
set -euo pipefail

VERSION="${1:-dev}"
OUT="release/$VERSION"
mkdir -p "$OUT"

# Detect current platform
OS="$(uname -s)"
ARCH="$(uname -m)"
case "$OS-$ARCH" in
  Darwin-arm64)  RID="osx-arm64"   ;;
  Darwin-x86_64) RID="osx-x64"     ;;
  Linux-aarch64) RID="linux-arm64" ;;
  Linux-x86_64)  RID="linux-x64"   ;;
  *)
    echo "Run on Windows with: deploy\build-release.ps1"
    exit 1
    ;;
esac

FLAGS="-c Release -r $RID --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded"

echo "Building federation-relay ($RID)..."
# shellcheck disable=SC2086
dotnet publish src/Federation.Relay $FLAGS -o "$OUT/relay"

echo "Building federation CLI ($RID)..."
# shellcheck disable=SC2086
dotnet publish src/Federation.Cli   $FLAGS -o "$OUT/cli"

echo ""
echo "Binaries in $OUT/"
ls -lh "$OUT/relay/federation-relay" "$OUT/cli/federation" 2>/dev/null || \
ls -lh "$OUT/relay/"                  "$OUT/cli/"
