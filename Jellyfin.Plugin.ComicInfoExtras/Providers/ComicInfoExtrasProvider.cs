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
/// Adds Tags, AgeRating, CommunityRating and SeriesGroup from ComicInfo.xml to books, after the built-in providers.
/// As an <see cref="ICustomMetadataProvider{TItemType}"/> it runs alongside the built-in Comic Provider
/// instead of replacing it.
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
            // Only clean up collections, in case the book used to belong to a group.
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
        changed |= ApplyCommunityRating(item, extras, config);

        if (UsesCollection(config.SeriesGroupMode))
        {
            // The collection is saved separately; the book itself does not change because of this.
            await _collections.SyncAsync(item, extras.SeriesGroup, cancellationToken).ConfigureAwait(false);
        }

        if (changed)
        {
            _logger.LogDebug("ComicInfo extras applied for {Path}", item.Path);
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
    /// Calculates the new tag list. The plugin's SeriesGroup tags are replaced every time,
    /// so that a changed group does not leave an old tag behind.
    /// </summary>
    /// <param name="existing">Existing tags.</param>
    /// <param name="extras">Values from ComicInfo.</param>
    /// <param name="config">Configuration.</param>
    /// <returns>The new tag list.</returns>
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

    /// <summary>
    /// Converts ComicInfo's 0 to 5 CommunityRating to Jellyfin's 0 to 10 scale.
    /// </summary>
    /// <param name="comicInfoRating">Value from ComicInfo, or null.</param>
    /// <returns>The rating on Jellyfin's scale, or null.</returns>
    internal static float? ToCommunityRating(double? comicInfoRating)
    {
        return comicInfoRating is null ? null : (float)(comicInfoRating.Value * 2);
    }

    private static bool ApplyCommunityRating(BaseItem item, ComicInfoFields extras, PluginConfiguration config)
    {
        var rating = ToCommunityRating(extras.CommunityRating);
        if (!config.ImportCommunityRating || rating is null || item.CommunityRating == rating)
        {
            return false;
        }

        item.CommunityRating = rating;
        return true;
    }

    private static bool UsesTag(SeriesGroupMode mode) => mode is SeriesGroupMode.Tag or SeriesGroupMode.TagAndCollection;

    private static bool UsesCollection(SeriesGroupMode mode) => mode is SeriesGroupMode.Collection or SeriesGroupMode.TagAndCollection;
}
