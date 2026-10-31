using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Jellyfin.Plugin.ComicInfoExtras.ComicInfo;
using Jellyfin.Plugin.ComicInfoExtras.Pages;
using Xunit;

namespace Jellyfin.Plugin.ComicInfoExtras.Tests;

public class PageCountTests
{
    [Theory]
    [InlineData("42", 42)]
    [InlineData(" 7 ", 7)]
    [InlineData("0", null)]
    [InlineData("-3", null)]
    [InlineData("viele", null)]
    public void Parse_ReadsPageCount(string value, int? expected)
    {
        var extras = ComicInfoFields.Parse(XDocument.Parse($"<ComicInfo><PageCount>{value}</PageCount></ComicInfo>"));

        Assert.Equal(expected, extras.PageCount);
    }

    [Fact]
    public void CountImages_SkipsComicInfoFoldersAndOtherFiles()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in new[] { "ComicInfo.xml", "chapter1/", "chapter1/001.jpg", "chapter1/002.JPEG", "003.png", "004.webp", "Thumbs.db", "readme.txt" })
            {
                zip.CreateEntry(name);
            }
        }

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.Equal(4, PageCountService.CountImages(archive));
    }

    [Theory]
    [InlineData(".cbr", 240000L, 24)]
    [InlineData(".PDF", 1230000L, 123)]
    [InlineData(".epub", 10000000L, null)]
    [InlineData(".cbr", 0L, null)]
    [InlineData(".cbr", null, null)]
    public void FromRunTimeTicks_ReadsJellyfinsPageCount(string extension, long? ticks, int? expected)
    {
        Assert.Equal(expected, PageCountService.FromRunTimeTicks(extension, ticks));
    }

    [Theory]
    [InlineData("/web/index.html", "")]
    [InlineData("/web/", "")]
    [InlineData("/jellyfin/web/index.html", "/jellyfin")]
    [InlineData("/Web/Index.html", "")]
    [InlineData("/web/main.bundle.js", null)]
    [InlineData("/Items/abc", null)]
    [InlineData("", null)]
    public void GetIndexPrefix_MatchesOnlyTheWebClientPage(string path, string? expected)
    {
        Assert.Equal(expected, WebScriptInjector.GetIndexPrefix(path));
    }

    [Fact]
    public void Inject_AddsScriptOnceBeforeHead()
    {
        var html = WebScriptInjector.Inject("<html><head><title>Jellyfin</title></head><body></body></html>", "/jellyfin");

        Assert.NotNull(html);
        Assert.Contains("src=\"/jellyfin/ComicInfoExtras/web/pageCount.js?v=", html, System.StringComparison.Ordinal);
        Assert.True(html!.IndexOf("pageCount.js", System.StringComparison.Ordinal) < html.IndexOf("</head>", System.StringComparison.Ordinal));
        Assert.Null(WebScriptInjector.Inject(html, "/jellyfin"));
        Assert.Null(WebScriptInjector.Inject("<html><body></body></html>", string.Empty));
    }
}
