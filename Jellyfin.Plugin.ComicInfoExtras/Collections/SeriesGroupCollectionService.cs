using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicInfoExtras.Collections;

/// <summary>
/// Keeps books in collections named after ComicInfo/SeriesGroup.
/// Collections created by the plugin carry their own provider ID, so they can be told apart from
/// manually maintained collections with the same name.
/// </summary>
public class SeriesGroupCollectionService
{
    /// <summary>
    /// Provider ID key the plugin uses to mark its collections.
    /// </summary>
    public const string ProviderKey = "ComicInfoSeriesGroup";

    // Books are updated in parallel; without a lock, duplicate collections would be created.
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ICollectionManager _collectionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesGroupCollectionService"/> class.
    /// </summary>
    /// <param name="collectionManager">Collection manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public SeriesGroupCollectionService(ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger logger)
    {
        _collectionManager = collectionManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Adds the book to the collection for its SeriesGroup and removes it from other plugin collections.
    /// </summary>
    /// <param name="item">The book.</param>
    /// <param name="seriesGroup">The SeriesGroup, or null if the book no longer belongs to any group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>true if something changed.</returns>
    public async Task<bool> SyncAsync(BaseItem item, string? seriesGroup, CancellationToken cancellationToken)
    {
        if (item.Id.Equals(Guid.Empty))
        {
            return false;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = false;
            var managed = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = [BaseItemKind.BoxSet],
                    CollapseBoxSetItems = false,
                    Recursive = true,
                })
                .OfType<BoxSet>()
                .Where(b => b.ProviderIds.ContainsKey(ProviderKey))
                .ToList();

            BoxSet? target = null;
            if (seriesGroup is not null)
            {
                target = managed.FirstOrDefault(b =>
                    string.Equals(b.ProviderIds[ProviderKey], seriesGroup, StringComparison.OrdinalIgnoreCase));

                if (target is null)
                {
                    _logger.LogInformation("Creating collection {Name} for SeriesGroup", seriesGroup);
                    target = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                    {
                        Name = seriesGroup,
                        IsLocked = false,
                        ProviderIds = new() { [ProviderKey] = seriesGroup },
                    }).ConfigureAwait(false);
                    changed = true;
                }

                if (!target.ContainsLinkedChildByItemId(item.Id))
                {
                    await _collectionManager.AddToCollectionAsync(target.Id, [item.Id]).ConfigureAwait(false);
                    changed = true;
                }
            }

            // If the SeriesGroup changed or was removed, take the book out of old plugin collections.
            foreach (var old in managed.Where(b => b.Id != target?.Id && b.ContainsLinkedChildByItemId(item.Id)))
            {
                await _collectionManager.RemoveFromCollectionAsync(old.Id, [item.Id]).ConfigureAwait(false);
                changed = true;
            }

            return changed;
        }
        catch (ArgumentException ex)
        {
            // E.g. when the book is not yet in the database during the first scan.
            _logger.LogDebug(ex, "Could not update collection for {Item}", item.Path);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
