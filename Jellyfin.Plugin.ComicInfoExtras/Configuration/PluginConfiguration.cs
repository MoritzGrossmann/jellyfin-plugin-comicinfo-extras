using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ComicInfoExtras.Configuration;

/// <summary>
/// Target system for AgeRating.
/// </summary>
public enum RatingSystem
{
    /// <summary>
    /// German FSK levels (FSK 0/6/12/16/18).
    /// </summary>
    Fsk,

    /// <summary>
    /// US levels (G, PG, PG-13, R, NC-17).
    /// </summary>
    Us,

    /// <summary>
    /// Pass the value through unchanged. Jellyfin's parental controls do not know most ComicInfo values.
    /// </summary>
    Original
}

/// <summary>
/// Where SeriesGroup is written to.
/// </summary>
public enum SeriesGroupMode
{
    /// <summary>
    /// Ignore SeriesGroup.
    /// </summary>
    Off,

    /// <summary>
    /// As a tag with prefix.
    /// </summary>
    Tag,

    /// <summary>
    /// As a collection (BoxSet).
    /// </summary>
    Collection,

    /// <summary>
    /// Tag and collection.
    /// </summary>
    TagAndCollection
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/Tags is imported.
    /// </summary>
    public bool ImportTags { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/AgeRating is imported.
    /// </summary>
    public bool ImportAgeRating { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/CommunityRating is imported.
    /// </summary>
    public bool ImportCommunityRating { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/Characters is imported as cast.
    /// </summary>
    public bool ImportCharacters { get; set; } = true;

    /// <summary>
    /// Gets or sets the target system for AgeRating.
    /// </summary>
    public RatingSystem RatingSystem { get; set; } = RatingSystem.Fsk;

    /// <summary>
    /// Gets or sets custom mappings, one per line in the format "ComicInfo value=Jellyfin rating".
    /// They take precedence over the built-in table.
    /// </summary>
    public string CustomRatingMappings { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where SeriesGroup is written to.
    /// </summary>
    public SeriesGroupMode SeriesGroupMode { get; set; } = SeriesGroupMode.Tag;

    /// <summary>
    /// Gets or sets the prefix for SeriesGroup tags.
    /// </summary>
    public string SeriesGroupTagPrefix { get; set; } = "SeriesGroup: ";
}
