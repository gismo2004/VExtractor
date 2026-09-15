#!/usr/bin/env bash
# Compile the executable from this checkout into build/bin/<runtime>/, the way a release does.
#
#   ./build.sh [runtime]    linux-x64 (default), linux-arm64, win-x64, osx-arm64, osx-x64
#
# Uses the host's .NET 8 SDK, or the SDK container when there is none. The result needs only the
# .NET 8 runtime to run: without arguments it is the guided build, `VExtractor help` lists the rest.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME="${1:-linux-x64}"
OUT="build/bin/$RUNTIME"
# The release workflow's switches, so a local build behaves like a published one.
PUBLISH=(publish VExtractor/VExtractor.csproj -c Release --nologo -v quiet
    -r "$RUNTIME" --self-contained false
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
    -o "$OUT")

cd "$DIR"
echo "==> Publishing for $RUNTIME ..."
# A runtime alone answers to `dotnet` but cannot compile, so ask for an SDK specifically.
if dotnet --list-sdks 2>/dev/null | grep -q .; then
    dotnet "${PUBLISH[@]}"
else
    # As the calling user, so nothing under build/ or obj/ ends up owned by root; the SDK gets a
    # scratch home inside the container for its caches.
    docker run --rm --user "$(id -u):$(id -g)" \
        -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        -v "$DIR:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet "${PUBLISH[@]}"
fi
echo "==> $DIR/$OUT"
