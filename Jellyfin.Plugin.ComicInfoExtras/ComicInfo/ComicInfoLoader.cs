using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicInfoExtras.ComicInfo;

/// <summary>
/// Findet und lädt ComicInfo.xml nach denselben Regeln wie Jellyfin:
/// zuerst "&lt;Dateiname&gt;.xml" bzw. "ComicInfo.xml" daneben, sonst ComicInfo.xml im .cbz-Archiv.
/// </summary>
public class ComicInfoLoader
{
    /// <summary>
    /// Dateiname der ComicInfo-Metadaten.
    /// </summary>
    public const string ComicInfoFileName = "ComicInfo.xml";

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComicInfoLoader"/> class.
    /// </summary>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public ComicInfoLoader(IFileSystem fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Lädt das ComicInfo-Dokument für ein Buch.
    /// </summary>
    /// <param name="path">Pfad des Buchs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Das Dokument oder null.</returns>
    public async Task<XDocument?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Gleiche Reihenfolge wie Jellyfin (ApplicationHost): externe XML vor der im Archiv.
        return await LoadExternalAsync(path, cancellationToken).ConfigureAwait(false)
            ?? await LoadFromArchiveAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Liefert den spätesten Änderungszeitpunkt aller möglichen ComicInfo-Quellen.
    /// </summary>
    /// <param name="path">Pfad des Buchs.</param>
    /// <returns>Zeitpunkt (UTC) oder null, wenn keine Quelle existiert.</returns>
    public DateTime? GetLastModifiedUtc(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        DateTime? latest = null;

        var archive = GetArchive(path);
        if (archive is not null)
        {
            latest = _fileSystem.GetLastWriteTimeUtc(archive);
        }

        var external = GetExternalXml(path);
        if (external is not null)
        {
            var time = _fileSystem.GetLastWriteTimeUtc(external);
            if (latest is null || time > latest)
            {
                latest = time;
            }
        }

        return latest;
    }

    private async Task<XDocument?> LoadFromArchiveAsync(string path, CancellationToken cancellationToken)
    {
        var archive = GetArchive(path);
        if (archive is null)
        {
            return null;
        }

        try
        {
            using var zip = await ZipFile.OpenReadAsync(archive.FullName, cancellationToken).ConfigureAwait(false);

            // Jellyfin sucht nur im Wurzelverzeichnis; manche Tools legen die Datei in einen Unterordner.
            var entry = zip.GetEntry(ComicInfoFileName)
                ?? zip.Entries.FirstOrDefault(e => string.Equals(e.Name, ComicInfoFileName, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return null;
            }

            var stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ComicInfo.xml in {Path} konnte nicht gelesen werden", archive.FullName);
            return null;
        }
    }

    private async Task<XDocument?> LoadExternalAsync(string path, CancellationToken cancellationToken)
    {
        var file = GetExternalXml(path);
        if (file is null)
        {
            return null;
        }

        try
        {
            using var reader = XmlReader.Create(file.FullName, new XmlReaderSettings { Async = true });
            return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Externe ComicInfo-Datei {Path} konnte nicht gelesen werden", file.FullName);
            return null;
        }
    }

    private FileSystemMetadata? GetArchive(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".cbz", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var file = _fileSystem.GetFileInfo(path);
        return file.Exists && !file.IsDirectory ? file : null;
    }

    private FileSystemMetadata? GetExternalXml(string path)
    {
        var info = _fileSystem.GetFileSystemInfo(path);
        var directory = info.IsDirectory ? info.FullName : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        var sameName = _fileSystem.GetFileInfo(Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + ".xml"));
        if (sameName.Exists)
        {
            return sameName;
        }

        var generic = _fileSystem.GetFileInfo(Path.Combine(directory, ComicInfoFileName));
        return generic.Exists ? generic : null;
    }
}
