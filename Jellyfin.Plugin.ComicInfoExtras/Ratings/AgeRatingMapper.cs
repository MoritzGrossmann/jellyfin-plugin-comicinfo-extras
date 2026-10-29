using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;

namespace Jellyfin.Plugin.ComicInfoExtras.Ratings;

/// <summary>
/// Übersetzt ComicInfo-AgeRating-Werte in Ratings, die Jellyfins Jugendschutz kennt.
/// Zwischenstufen werden aufgerundet (z. B. "Everyone 10+" auf FSK 12), damit der Jugendschutz nicht zu lasch greift.
/// </summary>
public static class AgeRatingMapper
{
    // Mindestalter je ComicInfo-Wert (Schema v2.1). null = kein Rating.
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
    /// <param name="value">Wert aus ComicInfo/AgeRating.</param>
    /// <param name="system">Zielsystem.</param>
    /// <param name="customMappings">Eigene Zuordnungen ("Wert=Rating" je Zeile), haben Vorrang.</param>
    /// <returns>Das Rating oder null, wenn nichts gesetzt werden soll.</returns>
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
            // Unbekannter Wert: unverändert übernehmen, Jellyfin versucht ihn selbst aufzulösen.
            return value;
        }

        if (minimumAge is null)
        {
            return null;
        }

        return system == RatingSystem.Us ? ToUs(minimumAge.Value) : ToFsk(minimumAge.Value);
    }

    /// <summary>
    /// Liest eigene Zuordnungen. Leere Zeilen und Zeilen mit # werden ignoriert.
    /// Ein leerer Wert rechts ("Teen=") bedeutet: kein Rating setzen.
    /// </summary>
    /// <param name="text">Zuordnungen, eine pro Zeile.</param>
    /// <returns>Wörterbuch ComicInfo-Wert zu Rating.</returns>
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
