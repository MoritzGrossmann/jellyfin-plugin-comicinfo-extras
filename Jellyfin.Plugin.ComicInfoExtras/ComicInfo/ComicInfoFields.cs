using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Jellyfin.Plugin.ComicInfoExtras.ComicInfo;

/// <summary>
/// Die Felder aus ComicInfo.xml, die Jellyfin selbst nicht auswertet.
/// </summary>
/// <param name="Tags">Werte aus ComicInfo/Tags.</param>
/// <param name="AgeRating">Wert aus ComicInfo/AgeRating.</param>
/// <param name="SeriesGroup">Wert aus ComicInfo/SeriesGroup.</param>
public sealed record ComicInfoFields(IReadOnlyList<string> Tags, string? AgeRating, string? SeriesGroup)
{
    /// <summary>
    /// Gets a value indicating whether überhaupt etwas gefunden wurde.
    /// </summary>
    public bool IsEmpty => Tags.Count == 0 && AgeRating is null && SeriesGroup is null;

    /// <summary>
    /// Liest die Felder aus einem ComicInfo-Dokument. Namensräume werden ignoriert.
    /// </summary>
    /// <param name="xml">Das ComicInfo-Dokument.</param>
    /// <returns>Die gefundenen Felder.</returns>
    public static ComicInfoFields Parse(XDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var root = xml.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "ComicInfo", StringComparison.OrdinalIgnoreCase))
        {
            return new ComicInfoFields([], null, null);
        }

        return new ComicInfoFields(
            SplitList(Value(root, "Tags")),
            Value(root, "AgeRating"),
            Value(root, "SeriesGroup"));
    }

    private static string? Value(XElement root, string name)
    {
        var value = root.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string[] SplitList(string? value)
    {
        if (value is null)
        {
            return [];
        }

        // ComicInfo trennt Listen mit Kommas, manche Tagger nutzen Semikolons.
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
