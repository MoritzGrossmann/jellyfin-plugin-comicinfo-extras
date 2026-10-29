using Jellyfin.Plugin.ComicInfoExtras.Configuration;
using Jellyfin.Plugin.ComicInfoExtras.Ratings;
using Xunit;

namespace Jellyfin.Plugin.ComicInfoExtras.Tests;

public class AgeRatingMapperTests
{
    [Theory]
    [InlineData("Everyone", "FSK 0")]
    [InlineData("Kids to Adults", "FSK 6")]
    [InlineData("Everyone 10+", "FSK 12")]
    [InlineData("Teen", "FSK 16")]
    [InlineData("MA15+", "FSK 16")]
    [InlineData("Mature 17+", "FSK 18")]
    [InlineData("Adults Only 18+", "FSK 18")]
    [InlineData("teen", "FSK 16")]
    public void Map_Fsk(string value, string expected)
    {
        Assert.Equal(expected, AgeRatingMapper.Map(value, RatingSystem.Fsk));
    }

    [Theory]
    [InlineData("Everyone", "G")]
    [InlineData("Everyone 10+", "PG")]
    [InlineData("Teen", "PG-13")]
    [InlineData("Mature 17+", "R")]
    [InlineData("X18+", "NC-17")]
    public void Map_Us(string value, string expected)
    {
        Assert.Equal(expected, AgeRatingMapper.Map(value, RatingSystem.Us));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Rating Pending")]
    [InlineData("")]
    [InlineData(null)]
    public void Map_ReturnsNullForUnrated(string? value)
    {
        Assert.Null(AgeRatingMapper.Map(value, RatingSystem.Fsk));
        Assert.Null(AgeRatingMapper.Map(value, RatingSystem.Original));
    }

    [Fact]
    public void Map_KeepsUnknownValues()
    {
        Assert.Equal("FSK 12", AgeRatingMapper.Map("FSK 12", RatingSystem.Fsk));
    }

    [Fact]
    public void Map_CustomMappingsWin()
    {
        const string Custom = "# Kommentar\nTeen = FSK 12\r\nM=\n";

        Assert.Equal("FSK 12", AgeRatingMapper.Map("Teen", RatingSystem.Fsk, Custom));
        Assert.Null(AgeRatingMapper.Map("M", RatingSystem.Fsk, Custom));
        Assert.Equal("FSK 18", AgeRatingMapper.Map("Mature 17+", RatingSystem.Fsk, Custom));
    }
}
