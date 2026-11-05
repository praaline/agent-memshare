#!/usr/bin/env bash
# Installs memshare on Linux or macOS: downloads the release for this platform, checks it against the
# release's SHA256SUMS, then runs `memshare install` (which copies it to ~/.local/bin and sets up the
# coding agent).
#
#   curl -fsSL https://github.com/praaline/agent-memshare/releases/latest/download/install.sh | bash -s -- --machine laptop
#
# Options: --machine NAME (default: this computer's name), --version vX.Y.Z (default: the latest),
# --stores-root PATH. Environment: MEMSHARE_REPO (owner/repository) to install from a fork.
set -euo pipefail

REPO="${MEMSHARE_REPO:-praaline/agent-memshare}"
VERSION="latest"
MACHINE=""
EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --machine) MACHINE="${2:?--machine needs a name}"; shift 2 ;;
    --version) VERSION="${2:?--version needs a version}"; shift 2 ;;
    --stores-root) EXTRA+=(--stores-root "${2:?--stores-root needs a path}"); shift 2 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *) echo "unsupported OS: $(uname -s) (on Windows, use install.ps1)" >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  aarch64 | arm64) arch=arm64 ;;
  *) echo "unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

if [ -z "$MACHINE" ]; then
  # This computer's name as a machine name: lower case, letters, digits and hyphens.
  MACHINE="$(hostname -s 2>/dev/null || hostname)"
  MACHINE="$(printf '%s' "$MACHINE" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//' | cut -c1-31)"
fi

asset="memshare-$os-$arch.tar.gz"
# MEMSHARE_DOWNLOAD_BASE (tests only) replaces the release's download URL.
if [ -n "${MEMSHARE_DOWNLOAD_BASE:-}" ]; then base="$MEMSHARE_DOWNLOAD_BASE"
elif [ "$VERSION" = latest ]; then base="https://github.com/$REPO/releases/latest/download"; else base="https://github.com/$REPO/releases/download/$VERSION"; fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
echo "downloading $asset ($VERSION) from $REPO"
curl -fsSL -o "$tmp/$asset" "$base/$asset"
curl -fsSL -o "$tmp/SHA256SUMS" "$base/SHA256SUMS"

expected="$(awk -v f="$asset" '$2 == f || $2 == "*" f { print $1 }' "$tmp/SHA256SUMS")"
[ -n "$expected" ] || { echo "$asset is not listed in SHA256SUMS" >&2; exit 1; }
if command -v sha256sum >/dev/null; then actual="$(sha256sum "$tmp/$asset" | cut -d' ' -f1)"; else actual="$(shasum -a 256 "$tmp/$asset" | cut -d' ' -f1)"; fi
[ "$expected" = "$actual" ] || { echo "checksum mismatch for $asset: not installed" >&2; exit 1; }
echo "checksum ok"

tar -xzf "$tmp/$asset" -C "$tmp"
"$tmp/memshare" install --machine "$MACHINE" "${EXTRA[@]}"
