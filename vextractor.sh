#!/usr/bin/env sh
# Run this to build the controller catalog for the OptoV integration.
# Checks for the .NET runtime, the one thing VExtractor cannot report missing itself,
# then starts the guided run.

cd "$(dirname "$0")" || exit 1
echo
echo "VExtractor - builds the controller catalog for the OptoV integration."
echo

if ! { command -v dotnet >/dev/null 2>&1 && dotnet --list-runtimes 2>/dev/null | grep -q '^Microsoft.NETCore.App [89]\.'; }; then
    echo "The .NET 8 runtime is not installed. VExtractor cannot start without it."
    echo
    echo "  Ubuntu 24.04+ : sudo apt install -y dotnet-runtime-8.0"
    echo "  Fedora        : sudo dnf install -y dotnet-runtime-8.0"
    echo "  Arch          : sudo pacman -S --needed dotnet-runtime"
    echo "  Debian        : add Microsoft's package feed first, see the README"
    echo "  macOS         : brew install dotnet@8"
    echo
    echo "Then run this script again."
    exit 1
fi

if [ -f ./VExtractor ]; then
    [ -x ./VExtractor ] || chmod +x ./VExtractor
    exec ./VExtractor
fi
echo "VExtractor is not next to this script. Extract the whole release archive into one folder."
exit 1
