// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleBundleReaderTests : BaseTests
{
    private const string IndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" content=\"__NCODE_CSP_NONCE__\"></head><body></body></html>";

    private const string RebuiltIndexHtml = "<!doctype html><html><body>rebuilt</body></html>";

    private DirectoryInfo ContentRoot { get; } = Directory.CreateTempSubdirectory("console-");

    private string DistPath => Path.Combine(ContentRoot.FullName, "dist");

    private string IndexPath => Path.Combine(DistPath, "index.html");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ContentRoot.Delete(recursive: true);
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private ManagementConsoleBundleReader CreateReader(string bundleRootPath = "dist")
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns(ContentRoot.FullName).Verifiable();

        return new ManagementConsoleBundleReader(
            Options.Create(new ManagementConsoleOptions { BundleRootPath = bundleRootPath }),
            mockEnvironment.Object
        );
    }

    // A reader without a configured bundle never reads the content root, so its environment has no setup.
    private ManagementConsoleBundleReader CreateUnconfiguredReader(string? bundleRootPath)
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();

        return new ManagementConsoleBundleReader(
            Options.Create(new ManagementConsoleOptions { BundleRootPath = bundleRootPath }),
            mockEnvironment.Object
        );
    }

    private void WriteIndex(string html, DateTime lastWriteTimeUtc)
    {
        Directory.CreateDirectory(DistPath);
        File.WriteAllText(IndexPath, html, SecureEncoding.UTF8);
        File.SetLastWriteTimeUtc(IndexPath, lastWriteTimeUtc);
    }

    private void WriteAsset(string name, string contents)
    {
        var assetsPath = Path.Combine(DistPath, "assets");
        Directory.CreateDirectory(assetsPath);
        File.WriteAllText(Path.Combine(assetsPath, name), contents, SecureEncoding.UTF8);
    }

    #endregion

    #region AssetsDirectory Tests

    [Fact]
    public void AssetsDirectory_Always_IsTheAssetsPathWithoutItsLeadingSlash()
    {
        Assert.Equal("assets", ManagementConsoleBundleReader.AssetsDirectory);
        Assert.Equal(
            ManagementConsoleDefaults.AssetsPath,
            $"/{ManagementConsoleBundleReader.AssetsDirectory}"
        );
    }

    #endregion

    #region TryReadIndexAsync Tests

    [Fact]
    public async Task TryReadIndexAsync_WhenTheBundleExists_ReturnsTheIndexText()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var reader = CreateReader();

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Equal(IndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheBundleRootIsAbsolute_ReturnsTheIndexText()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var reader = CreateReader(DistPath);

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Equal(IndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheBundleIsMissing_ReturnsNull()
    {
        using var reader = CreateReader();

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryReadIndexAsync_WhenNoBundleIsConfigured_ReturnsNull(string? bundleRootPath)
    {
        using var reader = CreateUnconfiguredReader(bundleRootPath);

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexMetadataIsUnchanged_ReturnsTheCachedText()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        using var reader = CreateReader();
        await reader.TryReadIndexAsync(CancellationToken.None);

        // Same length and last-write time: only a cached copy can still return the original text.
        WriteIndex(
            IndexHtml.Replace("doctype", "DOCTYPE", StringComparison.Ordinal),
            lastWriteTimeUtc
        );

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Equal(IndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRewrittenWithANewLength_ReturnsTheNewText()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        using var reader = CreateReader();
        await reader.TryReadIndexAsync(CancellationToken.None);

        WriteIndex(RebuiltIndexHtml, lastWriteTimeUtc);

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Equal(RebuiltIndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRewrittenWithANewWriteTime_ReturnsTheNewText()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var reader = CreateReader();
        await reader.TryReadIndexAsync(CancellationToken.None);

        WriteIndex(
            IndexHtml.Replace("doctype", "DOCTYPE", StringComparison.Ordinal),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        );

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Contains("DOCTYPE", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryReadIndexAsync_WhenTheIndexIsRemoved_ReturnsNull()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using var reader = CreateReader();
        await reader.TryReadIndexAsync(CancellationToken.None);

        File.Delete(IndexPath);

        var html = await reader.TryReadIndexAsync(CancellationToken.None);

        Assert.Null(html);
    }

    #endregion

    #region TryGetAsset Tests

    [Fact]
    public void TryGetAsset_WhenTheAssetsDirectoryHasTheFile_ReturnsIt()
    {
        WriteAsset("index.js", "x");
        using var reader = CreateReader();

        var file = reader.TryGetAsset("index.js");

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
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteAsset(".hidden", "x");
        File.WriteAllText(
            Path.Combine(ContentRoot.FullName, "outside.js"),
            "x",
            SecureEncoding.UTF8
        );
        using var reader = CreateReader();

        var file = reader.TryGetAsset(assetPath);

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenNoBundleIsConfigured_ReturnsNull()
    {
        using var reader = CreateUnconfiguredReader(bundleRootPath: null);

        var file = reader.TryGetAsset("index.js");

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenTheBundleIsBuiltAfterTheReaderIsCreated_ReturnsTheFile()
    {
        using var reader = CreateReader();
        Assert.Null(reader.TryGetAsset("index.js"));

        WriteAsset("index.js", "x");

        Assert.NotNull(reader.TryGetAsset("index.js"));
    }

    #endregion

    #region RenderIndex Tests

    [Fact]
    public void RenderIndex_WhenThePageHasPlaceholders_ReplacesEveryPlaceholder()
    {
        const string html =
            "<meta content=\"__NCODE_CSP_NONCE__\"><script nonce=\"__NCODE_CSP_NONCE__\">";

        var rendered = ManagementConsoleBundleReader.RenderIndex(html, "abc");

        Assert.Equal("<meta content=\"abc\"><script nonce=\"abc\">", rendered);
    }

    [Fact]
    public void RenderIndex_WhenThePageHasNoPlaceholder_ReturnsThePageUnchanged()
    {
        var rendered = ManagementConsoleBundleReader.RenderIndex("<html></html>", "abc");

        Assert.Equal("<html></html>", rendered);
    }

    [BuiltConsoleBundleFact]
    public async Task RenderIndex_WhenGivenTheBuiltBundlesIndex_LeavesNoPlaceholderAndNoncesEveryScript()
    {
        var html = await File.ReadAllTextAsync(
            BuiltConsoleBundleFactAttribute.IndexPath ?? string.Empty,
            SecureEncoding.UTF8
        );

        var rendered = ManagementConsoleBundleReader.RenderIndex(html, "n0nce");

        Assert.DoesNotContain("__NCODE_CSP_NONCE__", rendered, StringComparison.Ordinal);
        Assert.Matches("<meta property=\"csp-nonce\"[^>]*nonce=\"n0nce\"", rendered);

        var scriptTags = Regex.Matches(rendered, "<script\\b[^>]*>");
        Assert.NotEmpty(scriptTags);
        Assert.All(
            scriptTags,
            tag => Assert.Contains("nonce=\"n0nce\"", tag.Value, StringComparison.Ordinal)
        );
    }

    #endregion
}
