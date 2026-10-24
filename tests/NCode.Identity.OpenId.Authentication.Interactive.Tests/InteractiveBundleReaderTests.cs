#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveBundleReaderTests : IDisposable
{
    private const string IndexHtml =
        "<!doctype html><html><head></head><body><div id=\"root\"></div></body></html>";

    // The same length as IndexHtml, so only the last-write time tells the two versions apart.
    private const string RebuiltIndexHtml =
        "<!doctype html><html><head></head><body><div id=\"root\"></div></body></HTML>";

    private DirectoryInfo BundleRoot { get; } = Directory.CreateTempSubdirectory("bundle-");

    private string IndexPath => Path.Combine(BundleRoot.FullName, "index.html");

    public void Dispose() => BundleRoot.Delete(recursive: true);

    #region Helpers

    private static InteractiveBundleReader CreateReader(string? bundleRootPath) =>
        new(
            Options.Create(new InteractiveAuthenticationOptions { BundleRootPath = bundleRootPath })
        );

    private void WriteIndex(string html, DateTime lastWriteTimeUtc)
    {
        File.WriteAllText(IndexPath, html);
        File.SetLastWriteTimeUtc(IndexPath, lastWriteTimeUtc);
    }

    #endregion

    #region TryReadShellAsync Tests

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexHasAMountPoint_ReturnsItSplitAtTheMountPoint()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.NotNull(shell);
        Assert.Equal("<!doctype html><html><head>", shell.Prelude.ToString());
        Assert.Equal("</head><body><div id=\"root\">", shell.Head.ToString());
        Assert.Equal("</div></body></html>", shell.Tail.ToString());
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexIsUnchanged_ReturnsTheCachedShell()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);
        var first = await reader.TryReadShellAsync(CancellationToken.None);

        var second = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexHasNoMountPoint_ReturnsNull()
    {
        WriteIndex(
            "<!doctype html><html><body></body></html>",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        var reader = CreateReader(BundleRoot.FullName);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.Null(shell);
    }

    [Fact]
    public async Task TryReadShellAsync_WhenNoBundleIsConfigured_ReturnsNull()
    {
        var reader = CreateReader(bundleRootPath: null);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.Null(shell);
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexIsMissing_ReturnsNull()
    {
        var reader = CreateReader(BundleRoot.FullName);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.Null(shell);
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexMetadataIsUnchanged_ReturnsTheCachedContents()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadShellAsync(CancellationToken.None);

        // Same length and last-write time: only a cached copy can still return the original contents.
        WriteIndex(RebuiltIndexHtml, lastWriteTimeUtc);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.NotNull(shell);
        Assert.Equal("</div></body></html>", shell.Tail.ToString());
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexIsRewritten_ReturnsTheNewContents()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadShellAsync(CancellationToken.None);

        WriteIndex(RebuiltIndexHtml, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.NotNull(shell);
        Assert.Equal("</div></body></HTML>", shell.Tail.ToString());
    }

    [Fact]
    public async Task TryReadShellAsync_WhenTheIndexIsRemoved_ReturnsNull()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadShellAsync(CancellationToken.None);

        File.Delete(IndexPath);

        var shell = await reader.TryReadShellAsync(CancellationToken.None);

        Assert.Null(shell);
    }

    #endregion

    #region TryGetAsset Tests

    [Fact]
    public void TryGetAsset_WhenTheAssetsDirectoryHasTheFile_ReturnsIt()
    {
        Directory.CreateDirectory(Path.Combine(BundleRoot.FullName, "assets"));
        File.WriteAllText(Path.Combine(BundleRoot.FullName, "assets", "index.js"), "x");
        using var reader = CreateReader(BundleRoot.FullName);

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
        // The bundle is nested, so the file outside it is still cleaned up with the test's directory.
        var bundleRootPath = Path.Combine(BundleRoot.FullName, "dist");
        Directory.CreateDirectory(Path.Combine(bundleRootPath, "assets"));
        File.WriteAllText(Path.Combine(bundleRootPath, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(bundleRootPath, "assets", ".hidden"), "x");
        File.WriteAllText(Path.Combine(BundleRoot.FullName, "outside.js"), "x");
        using var reader = CreateReader(bundleRootPath);

        var file = reader.TryGetAsset(assetPath);

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenNoBundleIsConfigured_ReturnsNull()
    {
        using var reader = CreateReader(null);

        var file = reader.TryGetAsset("index.js");

        Assert.Null(file);
    }

    [Fact]
    public void TryGetAsset_WhenTheBundleIsBuiltAfterTheReaderIsCreated_ReturnsTheFile()
    {
        var bundleRootPath = Path.Combine(BundleRoot.FullName, "dist");
        using var reader = CreateReader(bundleRootPath);
        Assert.Null(reader.TryGetAsset("index.js"));

        Directory.CreateDirectory(Path.Combine(bundleRootPath, "assets"));
        File.WriteAllText(Path.Combine(bundleRootPath, "assets", "index.js"), "x");

        Assert.NotNull(reader.TryGetAsset("index.js"));
    }

    #endregion
}
