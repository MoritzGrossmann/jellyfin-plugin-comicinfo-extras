using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ComicInfoExtras.ComicInfo;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ComicInfoExtras.Characters;

/// <summary>
/// Adds ComicInfo/Characters to books as actors.
/// A metadata provider cannot do this: Jellyfin writes the people of the built-in provider's result
/// after all custom providers have run and would discard them. So the characters are applied
/// shortly after a book has been saved.
/// </summary>
public sealed class CharacterSyncService : IHostedService, IDisposable
{
    /// <summary>
    /// Role under which characters are stored, so that they can be told apart from real actors.
    /// </summary>
    internal const string CharacterRole = "Character";

    // The people of a refresh are saved right after the item itself; wait until that has happened.
    private static readonly TimeSpan _delay = TimeSpan.FromSeconds(3);

    private readonly ILibraryManager _libraryManager;
    private readonly ComicInfoLoader _loader;
    private readonly ILogger<CharacterSyncService> _logger;
    private readonly Channel<(Guid Id, DateTime Due)> _queue = Channel.CreateUnbounded<(Guid, DateTime)>();
    private readonly CancellationTokenSource _stopping = new();
    private Task? _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterSyncService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public CharacterSyncService(ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<CharacterSyncService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _loader = new ComicInfoLoader(fileSystem, logger);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        _worker = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_worker is not null)
        {
            await Task.WhenAny(_worker, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _stopping.Dispose();

    /// <summary>
    /// Calculates the new people list. Characters are stored as actors with the role <see cref="CharacterRole"/>;
    /// those entries are replaced every time, all other people are left alone.
    /// </summary>
    /// <param name="existing">Existing people.</param>
    /// <param name="extras">Values from ComicInfo.</param>
    /// <param name="config">Configuration.</param>
    /// <returns>The new people list.</returns>
    internal static List<PersonInfo> BuildPeople(IReadOnlyList<PersonInfo> existing, ComicInfoFields extras, PluginConfiguration config)
    {
        var result = existing.Where(p => !IsCharacter(p)).ToList();
        if (!config.ImportCharacters || extras.Characters is null)
        {
            return result;
        }

        var sortOrder = result.Count;
        foreach (var name in extras.Characters)
        {
            // Skip names that are already listed as an actor.
            if (result.Any(p => p.Type == PersonKind.Actor && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(new PersonInfo { Name = name, Type = PersonKind.Actor, Role = CharacterRole, SortOrder = sortOrder++ });
        }

        return result;
    }

    private static bool IsCharacter(PersonInfo person) =>
        person.Type == PersonKind.Actor && string.Equals(person.Role, CharacterRole, StringComparison.Ordinal);

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is Book)
        {
            _queue.Writer.TryWrite((e.Item.Id, DateTime.UtcNow + _delay));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var (id, due) in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var wait = due - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    await ApplyAsync(id, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not apply ComicInfo characters to item {Id}", id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Server is shutting down.
        }
    }

    private async Task ApplyAsync(Guid id, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (_libraryManager.GetItemById(id) is not Book book || book.LockedFields.Contains(MetadataField.Cast))
        {
            return;
        }

        var xml = await _loader.LoadAsync(book.Path, cancellationToken).ConfigureAwait(false);
        if (xml is null)
        {
            return;
        }

        var existing = _libraryManager.GetPeople(book);
        var people = BuildPeople(existing, ComicInfoFields.Parse(xml), config);
        var unchanged = people.Count == existing.Count
            && people.Zip(existing).All(x => x.First.Type == x.Second.Type
                && string.Equals(x.First.Name, x.Second.Name, StringComparison.Ordinal)
                && string.Equals(x.First.Role, x.Second.Role, StringComparison.Ordinal));
        if (unchanged)
        {
            return;
        }

        await _libraryManager.UpdatePeopleAsync(book, people, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("ComicInfo characters applied for {Path}", book.Path);
    }
}
