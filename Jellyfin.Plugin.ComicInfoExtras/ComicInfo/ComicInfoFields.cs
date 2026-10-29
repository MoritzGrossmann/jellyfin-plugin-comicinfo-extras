using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Jellyfin.Plugin.ComicInfoExtras.ComicInfo;

/// <summary>
/// The fields from ComicInfo.xml that Jellyfin itself does not read.
/// </summary>
/// <param name="Tags">Values from ComicInfo/Tags.</param>
/// <param name="AgeRating">Value from ComicInfo/AgeRating.</param>
/// <param name="SeriesGroup">Value from ComicInfo/SeriesGroup.</param>
public sealed record ComicInfoFields(IReadOnlyList<string> Tags, string? AgeRating, string? SeriesGroup)
{
    /// <summary>
    /// Gets a value indicating whether nothing at all was found.
    /// </summary>
    public bool IsEmpty => Tags.Count == 0 && AgeRating is null && SeriesGroup is null;

    /// <summary>
    /// Reads the fields from a ComicInfo document. Namespaces are ignored.
    /// </summary>
    /// <param name="xml">The ComicInfo document.</param>
    /// <returns>The fields that were found.</returns>
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

        // ComicInfo separates lists with commas, some taggers use semicolons.
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
