# The guided run in a container, for people who would rather not install the .NET runtime.
#
#   docker run --rm -it -v "$PWD:/data" ghcr.io/gismo2004/vextractor
#
# with the service software installer in the current folder; the catalog lands next to it.
# The image holds this repository's program, the .NET 8 runtime and Debian's 7-Zip, nothing
# else: the installer stays on the user's side of the mount and nothing from it is kept.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY VExtractor/ VExtractor/
# Debian's arch names to .NET's; the image is built for amd64 and arm64.
RUN case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) echo "unsupported: $TARGETARCH" && exit 1 ;; esac \
    && dotnet publish VExtractor/VExtractor.csproj -c Release --nologo -v quiet \
       -r "$rid" --self-contained false -p:DebugType=none -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0
# 7-Zip opens the installer; the package provides it as 7zz, which InstallerReader looks for.
RUN apt-get update && apt-get install -y --no-install-recommends 7zip \
    && rm -rf /var/lib/apt/lists/*
# As a plain directory, not the single file the release archives use: that file unpacks
# itself into the user's home at start, and a container user has none.
COPY --from=build /out/ /app/
# The user's folder: the installer is looked for here and the catalog written here, whoever
# the container runs as. The launcher flag skips the "press Enter to close" meant for a window
# that would vanish, which a terminal does not.
ENV VEXTRACTOR_HOME=/data VEXTRACTOR_LAUNCHER=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME /data
WORKDIR /data
ENTRYPOINT ["dotnet", "/app/VExtractor.dll"]
