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
        "<!doctype html><html><body><div id=\"root\"></div></body></html>";

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

    #region TryReadIndexHtmlAsync Tests

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenNoBundleIsConfigured_ReturnsNull()
    {
        var reader = CreateReader(bundleRootPath: null);

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenTheIndexIsMissing_ReturnsNull()
    {
        var reader = CreateReader(BundleRoot.FullName);

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenTheIndexExists_ReturnsItsContents()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Equal(IndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenTheIndexIsUnchanged_ReturnsTheCachedContents()
    {
        var lastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteIndex(IndexHtml, lastWriteTimeUtc);
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        // Same length and last-write time: only a cached copy can still return the original contents.
        WriteIndex(IndexHtml.ToUpperInvariant(), lastWriteTimeUtc);

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Equal(IndexHtml, html);
    }

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenTheIndexIsRewritten_ReturnsTheNewContents()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        var rebuilt = IndexHtml.ToUpperInvariant();
        WriteIndex(rebuilt, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Equal(rebuilt, html);
    }

    [Fact]
    public async Task TryReadIndexHtmlAsync_WhenTheIndexIsRemoved_ReturnsNull()
    {
        WriteIndex(IndexHtml, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var reader = CreateReader(BundleRoot.FullName);
        await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        File.Delete(IndexPath);

        var html = await reader.TryReadIndexHtmlAsync(CancellationToken.None);

        Assert.Null(html);
    }

    #endregion
}
