#!/bin/bash
# Packs the Windows MSIX packages by driving tools/build_windows.ps1 inside the
# Parallels Windows VM. MSIX can only be assembled on Windows, so the Mac builds
# everything else itself.
#
#   tools/build_windows.sh <output directory>
#
# The VM is picked by name (PSS_WINDOWS_VM overrides it) and must be running with
# Parallels Tools installed.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=${1:-Releases}
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)
BUILD=$(sed -n 's:.*<BuildNumber>\(.*\)</BuildNumber>.*:\1:p' Directory.Build.props | head -1)

if [ -n "${PSS_WINDOWS_VM:-}" ]; then
    VM=$PSS_WINDOWS_VM
else
    VM=$(prlctl list -a --no-header -o uuid,name | awk '/Windows/ {print $1; exit}')
fi
[ -n "$VM" ] || { echo "no Windows VM found; skipping MSIX" >&2; exit 1; }

# The VM sees the Mac home directory as \\Mac\Home.
REPO_WIN=$(printf '\\\\Mac\\Home%s' "${PWD#$HOME}" | sed 's:/:\\:g')
WIN_SOURCE=$REPO_WIN
WIN_OUT="$REPO_WIN\\$OUT"

echo "== Windows VM $VM"
prlctl exec "$VM" powershell -NoProfile -ExecutionPolicy Bypass \
    -File "$WIN_SOURCE\\tools\\build_windows.ps1" \
    -Source "$WIN_SOURCE" -Out "$WIN_OUT" -Version "$VERSION" -Build "$BUILD"
