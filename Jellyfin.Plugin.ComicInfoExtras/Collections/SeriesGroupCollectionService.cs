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
/// Hält Bücher in Sammlungen, die nach ComicInfo/SeriesGroup benannt sind.
/// Vom Plugin angelegte Sammlungen tragen eine eigene Provider-ID, damit sie von
/// handgepflegten Sammlungen gleichen Namens unterscheidbar sind.
/// </summary>
public class SeriesGroupCollectionService
{
    /// <summary>
    /// Provider-ID-Schlüssel, mit dem das Plugin seine Sammlungen markiert.
    /// </summary>
    public const string ProviderKey = "ComicInfoSeriesGroup";

    // Bücher werden parallel aktualisiert; ohne Sperre entstünden doppelte Sammlungen.
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
    /// Ordnet das Buch der Sammlung seiner SeriesGroup zu und entfernt es aus anderen Plugin-Sammlungen.
    /// </summary>
    /// <param name="item">Das Buch.</param>
    /// <param name="seriesGroup">Die SeriesGroup oder null, wenn das Buch keiner Gruppe (mehr) angehört.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>true, wenn sich etwas geändert hat.</returns>
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
                    _logger.LogInformation("Lege Sammlung {Name} für SeriesGroup an", seriesGroup);
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

            // Bei geänderter oder entfernter SeriesGroup aus alten Plugin-Sammlungen herausnehmen.
            foreach (var old in managed.Where(b => b.Id != target?.Id && b.ContainsLinkedChildByItemId(item.Id)))
            {
                await _collectionManager.RemoveFromCollectionAsync(old.Id, [item.Id]).ConfigureAwait(false);
                changed = true;
            }

            return changed;
        }
        catch (ArgumentException ex)
        {
            // z. B. wenn das Buch beim ersten Scan noch nicht in der Datenbank steht.
            _logger.LogDebug(ex, "Sammlung für {Item} konnte nicht aktualisiert werden", item.Path);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
