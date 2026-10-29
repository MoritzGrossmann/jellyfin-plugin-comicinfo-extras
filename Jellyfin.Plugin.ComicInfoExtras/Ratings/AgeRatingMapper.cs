using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;

namespace Jellyfin.Plugin.ComicInfoExtras.Ratings;

/// <summary>
/// Translates ComicInfo AgeRating values into ratings that Jellyfin's parental controls understand.
/// Intermediate levels are rounded up (e.g. "Everyone 10+" to FSK 12), so parental controls do not end up too lax.
/// </summary>
public static class AgeRatingMapper
{
    // Minimum age per ComicInfo value (schema v2.1). null = no rating.
    private static readonly Dictionary<string, int?> _minimumAge = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Unknown"] = null,
        ["Rating Pending"] = null,
        ["Early Childhood"] = 0,
        ["Everyone"] = 0,
        ["G"] = 0,
        ["Kids to Adults"] = 6,
        ["PG"] = 10,
        ["Everyone 10+"] = 10,
        ["E10+"] = 10,
        ["Teen"] = 13,
        ["M"] = 15,
        ["MA15+"] = 15,
        ["Mature 17+"] = 17,
        ["R18+"] = 18,
        ["X18+"] = 18,
        ["Adults Only 18+"] = 18,
    };

    /// <summary>
    /// Bildet einen ComicInfo-Wert auf ein Jellyfin-Rating ab.
    /// </summary>
    /// <param name="value">Value from ComicInfo/AgeRating.</param>
    /// <param name="system">Target system.</param>
    /// <param name="customMappings">Custom mappings ("value=rating" per line), which take precedence.</param>
    /// <returns>The rating, or null if nothing should be set.</returns>
    public static string? Map(string? value, RatingSystem system, string? customMappings = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        var custom = ParseCustomMappings(customMappings);
        if (custom.TryGetValue(value, out var customRating))
        {
            return string.IsNullOrEmpty(customRating) ? null : customRating;
        }

        if (system == RatingSystem.Original)
        {
            return _minimumAge.TryGetValue(value, out var age) && age is null ? null : value;
        }

        if (!_minimumAge.TryGetValue(value, out var minimumAge))
        {
            // Unknown value: pass it through unchanged, Jellyfin tries to resolve it itself.
            return value;
        }

        if (minimumAge is null)
        {
            return null;
        }

        return system == RatingSystem.Us ? ToUs(minimumAge.Value) : ToFsk(minimumAge.Value);
    }

    /// <summary>
    /// Reads custom mappings. Empty lines and lines starting with # are ignored.
    /// An empty value on the right ("Teen=") means: do not set a rating.
    /// </summary>
    /// <param name="text">Mappings, one per line.</param>
    /// <returns>Dictionary from ComicInfo value to rating.</returns>
    public static Dictionary<string, string> ParseCustomMappings(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return result;
    }

    private static string ToFsk(int age) => age switch
    {
        0 => "FSK 0",
        <= 6 => "FSK 6",
        <= 12 => "FSK 12",
        <= 16 => "FSK 16",
        _ => "FSK 18",
    };

    private static string ToUs(int age) => age switch
    {
        0 => "G",
        <= 10 => "PG",
        <= 13 => "PG-13",
        <= 17 => "R",
        _ => "NC-17",
    };
}
