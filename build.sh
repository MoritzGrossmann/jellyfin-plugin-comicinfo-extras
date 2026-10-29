#!/usr/bin/env sh
# Baut, testet und paketiert das Plugin im offiziellen .NET-10-SDK-Container.
# Ergebnis: dist/comicinfo-extras_<version>.zip (DLL + meta.json)
#
# Andere Jellyfin-Version:  JELLYFIN_VERSION=12.4.0 ./build.sh
set -eu

JELLYFIN_VERSION="${JELLYFIN_VERSION:-12.2.0}"
PLUGIN_VERSION="1.0.0.0"

docker run --rm \
  -v "$(pwd)":/src -w /src \
  -e JELLYFIN_VERSION="$JELLYFIN_VERSION" -e PLUGIN_VERSION="$PLUGIN_VERSION" \
  mcr.microsoft.com/dotnet/sdk:10.0 sh scripts/package.sh
