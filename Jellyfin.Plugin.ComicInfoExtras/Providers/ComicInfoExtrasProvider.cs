using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicInfoExtras.Collections;
using Jellyfin.Plugin.ComicInfoExtras.ComicInfo;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;
using Jellyfin.Plugin.ComicInfoExtras.Ratings;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicInfoExtras.Providers;

/// <summary>
/// Ergänzt Bücher nach den eingebauten Providern um Tags, AgeRating und SeriesGroup aus ComicInfo.xml.
/// Als <see cref="ICustomMetadataProvider{TItemType}"/> läuft er zusätzlich zum eingebauten Comic Provider,
/// statt ihn zu ersetzen.
/// </summary>
public class ComicInfoExtrasProvider : ICustomMetadataProvider<Book>, IHasItemChangeMonitor
{
    private readonly ComicInfoLoader _loader;
    private readonly SeriesGroupCollectionService _collections;
    private readonly ILogger<ComicInfoExtrasProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComicInfoExtrasProvider"/> class.
    /// </summary>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public ComicInfoExtrasProvider(
        IFileSystem fileSystem,
        ICollectionManager collectionManager,
        ILibraryManager libraryManager,
        ILogger<ComicInfoExtrasProvider> logger)
    {
        _logger = logger;
        _loader = new ComicInfoLoader(fileSystem, logger);
        _collections = new SeriesGroupCollectionService(collectionManager, libraryManager, logger);
    }

    /// <inheritdoc />
    public string Name => "ComicInfo Extras";

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(Book item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var xml = await _loader.LoadAsync(item.Path, cancellationToken).ConfigureAwait(false);
        if (xml is null)
        {
            // Nur Sammlungen aufräumen, falls das Buch früher einer Gruppe angehörte.
            if (UsesCollection(config.SeriesGroupMode))
            {
                await _collections.SyncAsync(item, null, cancellationToken).ConfigureAwait(false);
            }

            return ItemUpdateType.None;
        }

        var extras = ComicInfoFields.Parse(xml);
        var changed = false;
        changed |= ApplyTags(item, extras, config);
        changed |= ApplyAgeRating(item, extras, config);

        if (UsesCollection(config.SeriesGroupMode))
        {
            // Die Sammlung wird separat gespeichert; das Buch selbst ändert sich dadurch nicht.
            await _collections.SyncAsync(item, extras.SeriesGroup, cancellationToken).ConfigureAwait(false);
        }

        if (changed)
        {
            _logger.LogDebug("ComicInfo-Extras für {Path} übernommen", item.Path);
        }

        return changed ? ItemUpdateType.MetadataEdit : ItemUpdateType.None;
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        var lastModified = _loader.GetLastModifiedUtc(item.Path);
        return lastModified is not null && lastModified > item.DateLastSaved;
    }

    /// <summary>
    /// Berechnet die neue Tag-Liste. SeriesGroup-Tags des Plugins werden jedes Mal ersetzt,
    /// damit eine geänderte Gruppe keinen alten Tag hinterlässt.
    /// </summary>
    /// <param name="existing">Vorhandene Tags.</param>
    /// <param name="extras">Werte aus ComicInfo.</param>
    /// <param name="config">Konfiguration.</param>
    /// <returns>Die neue Tag-Liste.</returns>
    internal static string[] BuildTags(IReadOnlyList<string> existing, ComicInfoFields extras, PluginConfiguration config)
    {
        var prefix = config.SeriesGroupTagPrefix ?? string.Empty;
        var manageGroupTag = UsesTag(config.SeriesGroupMode) && prefix.Length > 0;

        IEnumerable<string> result = existing;

        if (manageGroupTag)
        {
            result = result.Where(t => !t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        if (config.ImportTags)
        {
            result = result.Concat(extras.Tags);
        }

        if (UsesTag(config.SeriesGroupMode) && extras.SeriesGroup is not null)
        {
            result = result.Append(prefix + extras.SeriesGroup);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool ApplyTags(Book item, ComicInfoFields extras, PluginConfiguration config)
    {
        if (item.LockedFields.Contains(MetadataField.Tags))
        {
            return false;
        }

        var newTags = BuildTags(item.Tags, extras, config);
        if (newTags.SequenceEqual(item.Tags, StringComparer.Ordinal))
        {
            return false;
        }

        item.Tags = newTags;
        return true;
    }

    private static bool ApplyAgeRating(Book item, ComicInfoFields extras, PluginConfiguration config)
    {
        if (!config.ImportAgeRating || item.LockedFields.Contains(MetadataField.OfficialRating))
        {
            return false;
        }

        var rating = AgeRatingMapper.Map(extras.AgeRating, config.RatingSystem, config.CustomRatingMappings);
        if (rating is null || string.Equals(rating, item.OfficialRating, StringComparison.Ordinal))
        {
            return false;
        }

        item.OfficialRating = rating;
        return true;
    }

    private static bool UsesTag(SeriesGroupMode mode) => mode is SeriesGroupMode.Tag or SeriesGroupMode.TagAndCollection;

    private static bool UsesCollection(SeriesGroupMode mode) => mode is SeriesGroupMode.Collection or SeriesGroupMode.TagAndCollection;
}
