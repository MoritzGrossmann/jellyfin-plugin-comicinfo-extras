using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ComicInfoExtras.Configuration;

/// <summary>
/// Zielsystem für AgeRating.
/// </summary>
public enum RatingSystem
{
    /// <summary>
    /// Deutsche FSK-Stufen (FSK 0/6/12/16/18).
    /// </summary>
    Fsk,

    /// <summary>
    /// US-Stufen (G, PG, PG-13, R, NC-17).
    /// </summary>
    Us,

    /// <summary>
    /// Wert unverändert übernehmen. Jellyfins Jugendschutz kennt die meisten ComicInfo-Werte nicht.
    /// </summary>
    Original
}

/// <summary>
/// Wohin SeriesGroup geschrieben wird.
/// </summary>
public enum SeriesGroupMode
{
    /// <summary>
    /// SeriesGroup ignorieren.
    /// </summary>
    Off,

    /// <summary>
    /// Als Tag mit Präfix.
    /// </summary>
    Tag,

    /// <summary>
    /// Als Sammlung (BoxSet).
    /// </summary>
    Collection,

    /// <summary>
    /// Tag und Sammlung.
    /// </summary>
    TagAndCollection
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/Tags importiert wird.
    /// </summary>
    public bool ImportTags { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ComicInfo/AgeRating importiert wird.
    /// </summary>
    public bool ImportAgeRating { get; set; } = true;

    /// <summary>
    /// Gets or sets das Zielsystem für AgeRating.
    /// </summary>
    public RatingSystem RatingSystem { get; set; } = RatingSystem.Fsk;

    /// <summary>
    /// Gets or sets eigene Zuordnungen, eine pro Zeile im Format "ComicInfo-Wert=Jellyfin-Rating".
    /// Sie haben Vorrang vor der eingebauten Tabelle.
    /// </summary>
    public string CustomRatingMappings { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets, wohin SeriesGroup geschrieben wird.
    /// </summary>
    public SeriesGroupMode SeriesGroupMode { get; set; } = SeriesGroupMode.Tag;

    /// <summary>
    /// Gets or sets das Präfix für SeriesGroup-Tags.
    /// </summary>
    public string SeriesGroupTagPrefix { get; set; } = "SeriesGroup: ";
}
