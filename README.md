# ComicInfo Extras – Jellyfin-Plugin

Jellyfin liest aus `ComicInfo.xml` nur Titel, Serie, Nummer, Zusammenfassung, Datum, Genre, Verlag, Sprache und Mitwirkende. Dieses Plugin ergänzt Bücher um drei weitere Felder:

| ComicInfo | Jellyfin | Verhalten |
|---|---|---|
| `Tags` | Tags | kommagetrennt (auch `;`), wird zu vorhandenen Tags hinzugefügt |
| `AgeRating` | Altersfreigabe | auf FSK- oder US-Stufen abgebildet, wirkt im Jugendschutz |
| `SeriesGroup` | Tag und/oder Sammlung | Tag mit Präfix (`SeriesGroup: Batman Family`) bzw. Sammlung gleichen Namens |

Gesperrte Felder (Tags, Altersfreigabe) im Metadaten-Editor werden respektiert.

## Wie es eingehängt ist

Das Plugin ist ein `ICustomMetadataProvider<Book>`. Es läuft **nach** Jellyfins eigenem „Comic Provider“ und ersetzt ihn nicht. Ein zusätzlicher `ILocalMetadataProvider` würde nicht funktionieren, weil Jellyfin nach dem ersten lokalen Provider mit Treffer abbricht.

Gesucht wird wie bei Jellyfin selbst:

1. `<Dateiname>.xml` neben dem Buch,
2. sonst `ComicInfo.xml` im selben Ordner,
3. sonst `ComicInfo.xml` im `.cbz`-Archiv (das Plugin sucht auch in Unterordnern des Archivs, Jellyfin selbst nur im Wurzelverzeichnis).

## AgeRating-Zuordnung

Zwischenstufen werden aufgerundet, damit der Jugendschutz eher zu streng als zu lasch greift.

| ComicInfo | Mindestalter | FSK | US |
|---|---|---|---|
| Early Childhood, Everyone, G | 0 | FSK 0 | G |
| Kids to Adults | 6 | FSK 6 | PG |
| PG, Everyone 10+, E10+ | 10 | FSK 12 | PG |
| Teen | 13 | FSK 16 | PG-13 |
| M, MA15+ | 15 | FSK 16 | R |
| Mature 17+ | 17 | FSK 18 | R |
| R18+, X18+, Adults Only 18+ | 18 | FSK 18 | NC-17 |
| Unknown, Rating Pending | – | nichts setzen | nichts setzen |

Unbekannte Werte (z. B. bereits `FSK 12`) werden unverändert übernommen. Eigene Zuordnungen in den Plugin-Einstellungen haben Vorrang, z. B. `Teen=FSK 12`.

## Bauen

Voraussetzung: Docker. Kein lokales .NET nötig.

Windows (PowerShell, Docker Desktop):

```powershell
.\build.ps1                            # gegen Jellyfin 12.2.0
.\build.ps1 -JellyfinVersion 12.4.0    # gegen eine andere Server-Version
```

Linux/macOS:

```sh
./build.sh                          # gegen Jellyfin 12.2.0
JELLYFIN_VERSION=12.4.0 ./build.sh  # gegen eine andere Server-Version
```

**Die Version muss zum Server passen oder älter sein.** Jellyfin lädt kein Plugin, dessen `targetAbi` höher ist als die eigene Version; es erscheint dann unter *Dashboard → Plugins* als nicht unterstützt.

Ergebnis: `dist/comicinfo-extras_1.0.0.0.zip` mit DLL und `meta.json`. Das Skript führt vorher die Unit-Tests aus.

Mit lokalem .NET-10-SDK geht auch:

```sh
dotnet test
dotnet publish Jellyfin.Plugin.ComicInfoExtras -c Release -o out
```

Die NuGet-Version von `Jellyfin.Controller`/`Jellyfin.Model` muss zur Server-Version passen (`JellyfinVersion` in `Directory.Build.props`), ebenso `targetAbi` in `build.yaml`.

## Installieren

1. ZIP in den Plugin-Ordner des Servers entpacken, in einen Unterordner `ComicInfo Extras_1.0.0.0`:
   - Windows (Installer/Dienst): `C:\ProgramData\Jellyfin\Server\plugins\ComicInfo Extras_1.0.0.0\`
   - Linux-Paket: `/var/lib/jellyfin/plugins/ComicInfo Extras_1.0.0.0/`
   - Docker: `/config/plugins/ComicInfo Extras_1.0.0.0/`
2. Jellyfin neu starten. Unter *Dashboard → Plugins* muss es mit Status **Aktiv** erscheinen.
3. Einstellungen prüfen (Freigabesystem, SeriesGroup-Modus).
4. Bücher-Bibliothek: *Metadaten aktualisieren → Alle Metadaten ersetzen*.

Der Provider taucht in den Bibliothekseinstellungen **nicht** auf und muss dort nicht aktiviert werden: Jellyfin listet dort nur lokale Leser, Online-Quellen und Speicher, Custom-Provider laufen immer.

## Grenzen

- Entfernte Einträge aus `<Tags>` bleiben als Jellyfin-Tag stehen, bis „Alle Metadaten ersetzen“ ausgeführt wird. SeriesGroup-Tags (mit Präfix) werden dagegen bei jeder Aktualisierung ersetzt.
- Wie bei Jellyfin selbst werden nur `.cbz`-Archive geöffnet. Für `.cbr`/`.cb7` funktioniert nur die externe XML-Datei.
- Sammlungen erkennt das Plugin an der Provider-ID `ComicInfoSeriesGroup`. Leer gewordene Sammlungen bleiben bestehen und können von Hand gelöscht werden.
