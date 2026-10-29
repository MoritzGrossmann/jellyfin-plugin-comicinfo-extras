using System.Xml.Linq;
using Jellyfin.Plugin.ComicInfoExtras.ComicInfo;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;
using Jellyfin.Plugin.ComicInfoExtras.Providers;
using Xunit;

namespace Jellyfin.Plugin.ComicInfoExtras.Tests;

public class ComicInfoExtrasTests
{
    private const string Sample = """
        <?xml version="1.0" encoding="utf-8"?>
        <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <Title>Der lange Halloween</Title>
          <Series>Batman</Series>
          <Tags>Detektiv, Noir; Klassiker , noir</Tags>
          <AgeRating>Teen</AgeRating>
          <SeriesGroup>Batman Family</SeriesGroup>
        </ComicInfo>
        """;

    [Fact]
    public void Parse_ReadsAllThreeFields()
    {
        var extras = ComicInfoFields.Parse(XDocument.Parse(Sample));

        Assert.Equal(new[] { "Detektiv", "Noir", "Klassiker" }, extras.Tags);
        Assert.Equal("Teen", extras.AgeRating);
        Assert.Equal("Batman Family", extras.SeriesGroup);
        Assert.False(extras.IsEmpty);
    }

    [Theory]
    [InlineData("4.13", 4.13)]
    [InlineData("4,13", 4.13)]
    [InlineData("7", 5.0)]
    [InlineData("-1", 0.0)]
    [InlineData("kaputt", null)]
    public void Parse_ReadsCommunityRatingOnComicInfoScale(string value, double? expected)
    {
        var extras = ComicInfoFields.Parse(XDocument.Parse($"<ComicInfo><CommunityRating>{value}</CommunityRating></ComicInfo>"));

        Assert.Equal(expected, extras.CommunityRating);
    }

    [Fact]
    public void ToCommunityRating_DoublesToJellyfinScale()
    {
        Assert.Equal(8.26f, ComicInfoExtrasProvider.ToCommunityRating(4.13)!.Value, 2);
        Assert.Null(ComicInfoExtrasProvider.ToCommunityRating(null));
    }

    [Fact]
    public void Parse_IgnoresEmptyAndMissingFields()
    {
        var extras = ComicInfoFields.Parse(XDocument.Parse("<ComicInfo><Tags>  </Tags></ComicInfo>"));

        Assert.True(extras.IsEmpty);
    }

    [Fact]
    public void Parse_IgnoresOtherRootElements()
    {
        var extras = ComicInfoFields.Parse(XDocument.Parse("<package><Tags>a</Tags></package>"));

        Assert.True(extras.IsEmpty);
    }

    [Fact]
    public void BuildTags_ReplacesOldSeriesGroupTag()
    {
        var config = new PluginConfiguration { SeriesGroupMode = SeriesGroupMode.Tag };
        var extras = new ComicInfoFields(["Noir"], null, "Neue Gruppe");

        var tags = ComicInfoExtrasProvider.BuildTags(["Eigener Tag", "SeriesGroup: Alte Gruppe"], extras, config);

        Assert.Equal(new[] { "Eigener Tag", "Noir", "SeriesGroup: Neue Gruppe" }, tags);
    }

    [Fact]
    public void BuildTags_RemovesGroupTagWhenGroupIsGone()
    {
        var config = new PluginConfiguration { SeriesGroupMode = SeriesGroupMode.Tag };
        var extras = new ComicInfoFields([], null, null);

        var tags = ComicInfoExtrasProvider.BuildTags(["SeriesGroup: Alte Gruppe"], extras, config);

        Assert.Empty(tags);
    }

    [Fact]
    public void BuildTags_LeavesGroupTagsAloneWhenModeIsOff()
    {
        var config = new PluginConfiguration { SeriesGroupMode = SeriesGroupMode.Off, ImportTags = false };
        var extras = new ComicInfoFields(["Noir"], null, "Gruppe");

        var tags = ComicInfoExtrasProvider.BuildTags(["SeriesGroup: Alt"], extras, config);

        Assert.Equal(new[] { "SeriesGroup: Alt" }, tags);
    }
}
