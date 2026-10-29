# Baut, testet und paketiert das Plugin im .NET-10-SDK-Container (Docker Desktop).
# Ergebnis: dist\comicinfo-extras_<version>.zip (DLL + meta.json)
#
# Aufruf:                 .\build.ps1
# Andere Jellyfin-Version: .\build.ps1 -JellyfinVersion 12.4.0
param(
    [string]$JellyfinVersion = "12.2.0",
    [string]$PluginVersion = "1.0.0.0"
)

$ErrorActionPreference = "Stop"

docker run --rm `
    -v "${PWD}:/src" -w /src `
    -e "JELLYFIN_VERSION=$JellyfinVersion" -e "PLUGIN_VERSION=$PluginVersion" `
    mcr.microsoft.com/dotnet/sdk:10.0 sh scripts/package.sh

if ($LASTEXITCODE -ne 0) {
    throw "Build fehlgeschlagen (Exit-Code $LASTEXITCODE)"
}
