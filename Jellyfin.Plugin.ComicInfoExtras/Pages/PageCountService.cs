using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicInfoExtras.ComicInfo;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicInfoExtras.Pages;

/// <summary>
/// Works out how many pages a book has, for the page count on the details page.
/// </summary>
public class PageCountService
{
    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".avif", ".jxl", ".tif", ".tiff", ".heic", ".heif"
    };

    // Formats for which Jellyfin's own probe stores the page count in RunTimeTicks.
    private static readonly HashSet<string> _probedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz", ".cbr", ".cb7", ".cbt", ".pdf"
    };

    private readonly ComicInfoLoader _loader;
    private readonly ILogger<PageCountService> _logger;
    private readonly ConcurrentDictionary<string, (DateTime Modified, int? Pages)> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PageCountService"/> class.
    /// </summary>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public PageCountService(IFileSystem fileSystem, ILogger<PageCountService> logger)
    {
        _logger = logger;
        _loader = new ComicInfoLoader(fileSystem, logger);
    }

    /// <summary>
    /// Gets the page count of a book. In order: images in a .cbz archive,
    /// ComicInfo/PageCount, then the count Jellyfin stored when it probed the file.
    /// </summary>
    /// <param name="book">The book.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page count, or null if it is unknown (e.g. for EPUB).</returns>
    public async Task<int?> GetPageCountAsync(Book book, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(book);

        var path = book.Path;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var modified = _loader.GetLastModifiedUtc(path) ?? DateTime.MinValue;
        if (_cache.TryGetValue(path, out var cached) && cached.Modified == modified)
        {
            return cached.Pages;
        }

        var pages = CountArchiveImages(path);
        if (pages is null)
        {
            var xml = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            pages = xml is null ? null : ComicInfoFields.Parse(xml).PageCount;
        }

        pages ??= FromRunTimeTicks(Path.GetExtension(path), book.RunTimeTicks);

        _cache[path] = (modified, pages);
        return pages;
    }

    /// <summary>
    /// Counts the image entries of a comic archive. ComicInfo.xml and other files are not pages.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The number of images.</returns>
    internal static int CountImages(ZipArchive archive)
    {
        return archive.Entries.Count(e => e.Name.Length > 0 && _imageExtensions.Contains(Path.GetExtension(e.Name)));
    }

    /// <summary>
    /// Reads the page count Jellyfin stores in RunTimeTicks (pages * 10000) for comics and PDFs.
    /// EPUBs get a placeholder value there, so they are skipped.
    /// </summary>
    /// <param name="extension">File extension of the book.</param>
    /// <param name="runTimeTicks">The item's RunTimeTicks.</param>
    /// <returns>The page count, or null.</returns>
    internal static int? FromRunTimeTicks(string extension, long? runTimeTicks)
    {
        if (runTimeTicks is not > 0 || !_probedExtensions.Contains(extension))
        {
            return null;
        }

        return (int)(runTimeTicks.Value / 10000);
    }

    private int? CountArchiveImages(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".cbz", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var count = CountImages(zip);
            return count > 0 ? count : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not count the pages of {Path}", path);
            return null;
        }
    }
}
