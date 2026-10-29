#!/usr/bin/env sh
# Tests and packages the plugin. Runs inside the .NET SDK container (see build.sh / build.ps1) or on a CI runner.
# The plugin version and changelog come from build.yaml.
set -eu
: "${JELLYFIN_VERSION:=12.2.0}"

PLUGIN_VERSION=$(sed -n 's/^version: *"\([0-9.]*\)".*/\1/p' build.yaml | head -n1)
# JSON-safe single line: backslashes dropped, double quotes become single quotes
CHANGELOG=$(sed -n '/^changelog:/,$p' build.yaml | sed '1d' | sed 's/^ *//' | paste -sd ' ' - | tr -d '\134' | tr '"' "'")
[ -n "$PLUGIN_VERSION" ] || { echo "No version found in build.yaml" >&2; exit 1; }

dotnet test Jellyfin.Plugin.ComicInfoExtras.Tests -p:JellyfinVersion=$JELLYFIN_VERSION
dotnet publish Jellyfin.Plugin.ComicInfoExtras -c Release -o out -p:JellyfinVersion=$JELLYFIN_VERSION
rm -rf dist/pkg && mkdir -p dist/pkg
cp out/Jellyfin.Plugin.ComicInfoExtras.dll dist/pkg/
cat > dist/pkg/meta.json <<JSON
{
  "category": "Metadata",
  "changelog": "${CHANGELOG}",
  "description": "Adds Tags, AgeRating, CommunityRating and SeriesGroup from ComicInfo.xml to books.",
  "imageUrl": "https://raw.githubusercontent.com/MoritzGrossmann/jellyfin-plugin-comicinfo-extras/master/images/comicinfo-extras.png",
  "guid": "3f05923b-afb5-4e96-9968-692f343449e2",
  "name": "ComicInfo Extras",
  "overview": "Tags, AgeRating, CommunityRating and SeriesGroup from ComicInfo.xml for books",
  "owner": "moritz",
  "targetAbi": "${JELLYFIN_VERSION}.0",
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "version": "${PLUGIN_VERSION}",
  "status": "Active",
  "autoUpdate": false,
  "assemblies": ["Jellyfin.Plugin.ComicInfoExtras.dll"]
}
JSON
if ! command -v zip >/dev/null 2>&1; then
  apt-get update -qq >/dev/null && apt-get install -y -qq zip >/dev/null
fi
rm -f dist/comicinfo-extras_${PLUGIN_VERSION}.zip
cd dist/pkg && zip -q ../comicinfo-extras_${PLUGIN_VERSION}.zip * && cd ../..
rm -rf dist/pkg out
chown -R $(stat -c %u:%g /src) dist 2>/dev/null || true
echo "Done: dist/comicinfo-extras_${PLUGIN_VERSION}.zip"
