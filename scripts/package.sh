#!/usr/bin/env sh
# Läuft im .NET-SDK-Container (siehe build.sh / build.ps1).
set -eu
dotnet test Jellyfin.Plugin.ComicInfoExtras.Tests -p:JellyfinVersion=$JELLYFIN_VERSION
dotnet publish Jellyfin.Plugin.ComicInfoExtras -c Release -o out -p:JellyfinVersion=$JELLYFIN_VERSION
rm -rf dist/pkg && mkdir -p dist/pkg
cp out/Jellyfin.Plugin.ComicInfoExtras.dll dist/pkg/
cat > dist/pkg/meta.json <<EOF
{
  "category": "Metadata",
  "changelog": "Erste Version.",
  "description": "Ergänzt Bücher um Tags, AgeRating und SeriesGroup aus ComicInfo.xml.",
  "guid": "3f05923b-afb5-4e96-9968-692f343449e2",
  "name": "ComicInfo Extras",
  "overview": "Tags, AgeRating und SeriesGroup aus ComicInfo.xml für Bücher",
  "owner": "moritz",
  "targetAbi": "${JELLYFIN_VERSION}.0",
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "version": "${PLUGIN_VERSION}",
  "status": "Active",
  "autoUpdate": false,
  "assemblies": ["Jellyfin.Plugin.ComicInfoExtras.dll"]
}
EOF
apt-get update -qq >/dev/null && apt-get install -y -qq zip >/dev/null
rm -f dist/comicinfo-extras_${PLUGIN_VERSION}.zip
cd dist/pkg && zip -q ../comicinfo-extras_${PLUGIN_VERSION}.zip * && cd ../..
rm -rf dist/pkg out
chown -R $(stat -c %u:%g /src) dist 2>/dev/null || true
echo "Fertig: dist/comicinfo-extras_${PLUGIN_VERSION}.zip"
  
