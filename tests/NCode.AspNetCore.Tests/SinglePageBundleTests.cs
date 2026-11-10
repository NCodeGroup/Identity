// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Buffers;
using Xunit;

namespace NCode.AspNetCore.Tests;

public class SinglePageBundleTests : BaseTests
{
    private const string IndexHtml =
        "<!doctype html><html><head></head><body><div id=\"root\"></div></body></html>";

    private const string RebuiltIndexHtml = "<!doctype html><html><body>rebuilt</body></html>";

    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("bundle-");

    private int ParseCalls { get; set; }

    private string DistPath => Path.Combine(Root.FullName, "dist");

    private string IndexPath => Path.Combine(DistPath, "index.html");

    private string AssetsPath => Path.Combine(DistPath, "assets");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Root.Delete(recursive: true);
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private SinglePageBundle<SinglePageTestIndex> CreateBundle(
        string? rootPath,
        Func<string, SinglePageTestIndex?>? parseIndex = null
    ) =>
        new(
            rootPath,
            html =>
            {
                ParseCalls++;
                return parseIndex is null ? new SinglePageTestIndex(html) : parseIndex(html);
            }
        );

    private SinglePageBundle<SinglePageTestIndex> CreateBundle() => CreateBundle(DistPath);

    private void WriteIndex(string html, DateTime lastWriteTimeUtc)
    {
        Directory.CreateDirectory(DistPath);
        File.WriteAllText(IndexPath, html, SecureEncoding.UTF8);
        File.SetLastWriteTimeUtc(IndexPath, lastWriteTimeUtc);
    }

    private void WriteAsset(string name, string contents)
    {
        Directory.CreateDirectory(AssetsPath);
        File.WriteAllText(Path.Combine(AssetsPath, name), contents, SecureEncoding.UTF8);
    }

    #endregion

    #region TryReadIndexAsync Tests

    [Fact]
    public async Task TryReadIndexAsync_WhenTheBundleExists_ReturnsTheParsedIndex()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle();

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.NotNull(index);
        Assert.Equal(IndexHtml, index.Html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheBundleIsMissing_ReturnsNull()
    {
        using var bundle = CreateBundle();

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(index);
        Assert.Equal(0, ParseCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryReadIndexAsync_WhenNoBundleIsConfigured_ReturnsNull(string? rootPath)
    {
        using var bundle = CreateBundle(rootPath);

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(index);
        Assert.Equal(0, ParseCalls);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsUnchanged_ReturnsTheCachedIndex()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle();
        var first = await bundle.TryReadIndexAsync(CancellationToken.None);

        var second = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsUnchanged_ParsesItOnce()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle();

        await bundle.TryReadIndexAsync(CancellationToken.None);
        await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.Equal(1, ParseCalls);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheParserFindsNoIndex_ReturnsNull()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle(DistPath, _ => null);

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(index);
        Assert.Equal(1, ParseCalls);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexMetadataIsUnchanged_ReturnsTheCachedIndex()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        using var bundle = CreateBundle();
        await bundle.TryReadIndexAsync(CancellationToken.None);

        // Same length and last-write time: only a cached copy can still return the original text.
        WriteIndex(
            IndexHtml.Replace("doctype", "DOCTYPE", StringComparison.Ordinal),
            lastWriteTimeUtc
        );

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.NotNull(index);
        Assert.Equal(IndexHtml, index.Html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRewrittenWithANewLength_ReturnsTheNewIndex()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        using var bundle = CreateBundle();
        await bundle.TryReadIndexAsync(CancellationToken.None);

        WriteIndex(RebuiltIndexHtml, lastWriteTimeUtc);

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.NotNull(index);
        Assert.Equal(RebuiltIndexHtml, index.Html);
        Assert.Equal(2, ParseCalls);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRewrittenWithANewWriteTime_ReturnsTheNewIndex()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle();
        await bundle.TryReadIndexAsync(CancellationToken.None);

        WriteIndex(
            IndexHtml.Replace("doctype", "DOCTYPE", StringComparison.Ordinal),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        );

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.NotNull(index);
        Assert.Contains("DOCTYPE", index.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRemoved_ReturnsNull()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var bundle = CreateBundle();
        await bundle.TryReadIndexAsync(CancellationToken.None);

        File.Delete(IndexPath);

        var index = await bundle.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(index);
    }

    #endregion

    #region TryGetAsset Tests

    [Fact]
    public void TryGetAsset_WhenTheAssetsDirectoryHasTheFile_ReturnsIt()
    {
        WriteAsset("index.js", "x");
        using var bundle = CreateBundle();

        var file = bundle.TryGetAsset("index.js");

        Assert.NotNull(file);
        Assert.Equal("index.js", file.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing.js")]
    [InlineData("../index.html")]
    [InlineData("../../outside.js")]
    [InlineData(".hidden")]
    public void TryGetAsset_WhenThePathNamesNoFileOfTheAssetsDirectory_ReturnsNull(string assetPath)
    {
        // The bundle is nested, so the file outside it is still cleaned up with the test's directory.
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteAsset(".hidden", "x");
        File.WriteAllText(Path.Combine(Root.FullName, "outside.js"), "x", SecureEncoding.UTF8);
        using var bundle = CreateBundle();

        var file = bundle.TryGetAsset(assetPath);

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenNoBundleIsConfigured_ReturnsNull()
    {
        using var bundle = CreateBundle(rootPath: null);

        var file = bundle.TryGetAsset("index.js");

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenTheBundleIsBuiltAfterTheBundleIsCreated_ReturnsTheFile()
    {
        using var bundle = CreateBundle();
        Assert.Null(bundle.TryGetAsset("index.js"));

        WriteAsset("index.js", "x");

        Assert.NotNull(bundle.TryGetAsset("index.js"));
    }

    #endregion

    #region ServeAsset Tests

    [Fact]
    public void ServeAsset_WhenTheAssetExists_ServesItWithAnImmutableCacheLifetime()
    {
        WriteAsset("index-abc123.js", "console.log('hi');");
        var assetPath = Path.Combine(AssetsPath, "index-abc123.js");
        using var bundle = CreateBundle();
        var httpContext = new DefaultHttpContext();

        var result = bundle.ServeAsset(httpContext.Response, "index-abc123.js");

        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.EndsWith("index-abc123.js", file.FileName, StringComparison.Ordinal);
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(assetPath)), file.LastModified);
        Assert.NotNull(file.EntityTag);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Theory]
    [InlineData("favicon-abc123.ico", "image/x-icon")]
    [InlineData("favicon-abc123.svg", "image/svg+xml")]
    [InlineData("apple-touch-icon-abc123.png", "image/png")]
    public void ServeAsset_WhenTheAssetIsAnIcon_ServesItWithItsImageType(
        string fileName,
        string contentType
    )
    {
        WriteAsset(fileName, "x");
        using var bundle = CreateBundle();

        var result = bundle.ServeAsset(new DefaultHttpContext().Response, fileName);

        Assert.Equal(contentType, Assert.IsType<PhysicalFileHttpResult>(result).ContentType);
    }

    [Fact]
    public void ServeAsset_WhenTheAssetIsMissing_AnswersNotFoundWithoutACacheLifetime()
    {
        WriteAsset("index-abc123.js", "console.log('hi');");
        using var bundle = CreateBundle();
        var httpContext = new DefaultHttpContext();

        var result = bundle.ServeAsset(httpContext.Response, "missing.js");

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void ServeAsset_WhenTheAssetHasAnUnknownType_AnswersNotFound()
    {
        WriteAsset("data.unknownext123", "x");
        using var bundle = CreateBundle();
        var httpContext = new DefaultHttpContext();

        var result = bundle.ServeAsset(httpContext.Response, "data.unknownext123");

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void ServeAsset_WithAnEmptyAssetPath_AnswersNotFound()
    {
        using var bundle = CreateBundle();
        var httpContext = new DefaultHttpContext();

        var result = bundle.ServeAsset(httpContext.Response, string.Empty);

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Cache-Control"));
    }

    #endregion
}
