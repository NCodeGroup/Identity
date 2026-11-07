// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;

namespace NCode.AspNetCore;

/// <summary>
/// Reads a single-page bundle's <c>index.html</c> and serves the files of its <c>assets</c> directory. The index is
/// parsed once per file version and cached; each request compares the file's last-write time and length with the
/// cached copy's, so a rebuilt bundle is picked up without a restart. Nothing outside the assets directory, including
/// the index, is served by <see cref="ServeAsset"/>.
/// </summary>
/// <typeparam name="TIndex">The type that the bundle's <c>index.html</c> parses into.</typeparam>
// See ADR-1457.
// The cache is revalidated from file metadata rather than invalidated by a file watcher: a watcher costs an inotify
// instance per process and misses changes on bind-mounted and network file systems, while one metadata read per
// request is far cheaper than reading the file.
[PublicAPI]
public sealed class SinglePageBundle<TIndex>(string? rootPath, Func<string, TIndex?> parseIndex)
    : IDisposable
    where TIndex : class
{
    // A file's name carries its content hash, so no copy of it can go stale: a browser keeps it for a year.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private static FileExtensionContentTypeProvider AssetContentTypes { get; } = new();

    private Func<string, TIndex?> ParseIndex { get; } =
        parseIndex ?? throw new ArgumentNullException(nameof(parseIndex));

    private string? IndexPath { get; } =
        string.IsNullOrWhiteSpace(rootPath)
            ? null
            : Path.Combine(rootPath, SinglePageBundleDefaults.IndexFileName);

    private string? AssetsRootPath { get; } =
        string.IsNullOrWhiteSpace(rootPath)
            ? null
            : Path.Combine(rootPath, SinglePageBundleDefaults.AssetsDirectory);

    // Rooted at the bundle's assets directory, so a requested asset path that navigates above it, or names a hidden or
    // system file, resolves to no file. Created once the directory exists, which a development build may create after
    // the host starts.
    private PhysicalFileProvider? _assetProvider;

    // Replaced as a whole, so a request sees either the previous or the new version, never a mix of the two.
    private SinglePageBundleIndex<TIndex>? CachedIndex { get; set; }

    /// <inheritdoc />
    public void Dispose() => _assetProvider?.Dispose();

    /// <summary>
    /// Gets a file of the bundle's assets directory by its path relative to that directory. A path that leaves the
    /// directory, or names a hidden or system file, resolves to no file.
    /// </summary>
    /// <param name="assetPath">The file's path relative to the assets directory, such as <c>index-abc123.js</c>.</param>
    /// <returns>The file, or <c>null</c> when no bundle is present or the assets directory has no such file.</returns>
    public IFileInfo? TryGetAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        var provider = Volatile.Read(ref _assetProvider) ?? TryCreateAssetProvider();
        var file = provider?.GetFileInfo(assetPath);
        return file is { Exists: true, IsDirectory: false, PhysicalPath: not null } ? file : null;
    }

    private PhysicalFileProvider? TryCreateAssetProvider()
    {
        if (string.IsNullOrEmpty(AssetsRootPath) || !Directory.Exists(AssetsRootPath))
        {
            return null;
        }

        var provider = new PhysicalFileProvider(AssetsRootPath);
        var existing = Interlocked.CompareExchange(ref _assetProvider, provider, null);
        if (existing is null)
        {
            return provider;
        }

        provider.Dispose();
        return existing;
    }

    /// <summary>
    /// Reads the bundle's <c>index.html</c> and parses it once per file version.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>
    /// The parsed index, or <see langword="null"/> when no bundle is configured or available, or the index file does
    /// not parse.
    /// </returns>
    public async ValueTask<TIndex?> TryReadIndexAsync(CancellationToken cancellationToken)
    {
        if (IndexPath is null)
        {
            return null;
        }

        var file = new FileInfo(IndexPath);
        if (!file.Exists)
        {
            return null;
        }

        var lastWriteTimeUtc = file.LastWriteTimeUtc;
        var length = file.Length;

        var cachedIndex = CachedIndex;
        if (
            cachedIndex is not null
            && cachedIndex.LastWriteTimeUtc == lastWriteTimeUtc
            && cachedIndex.Length == length
        )
        {
            return cachedIndex.Index;
        }

        // A rebuild that lands between the metadata read and this read is cached under the older metadata, so the
        // next request sees a mismatch and reads the file again.
        var html = await File.ReadAllTextAsync(IndexPath, SecureEncoding.UTF8, cancellationToken);
        var parsed = ParseIndex(html);
        CachedIndex = new SinglePageBundleIndex<TIndex>(lastWriteTimeUtc, length, parsed);
        return parsed;
    }

    /// <summary>
    /// Serves a file of the bundle's assets directory with a year-long immutable cache lifetime and validators. A path
    /// the directory has no file for, or whose type is unknown, is answered <c>404</c> without a cache lifetime.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> for the current request.</param>
    /// <param name="assetPath">The file's path relative to the assets directory, such as <c>index-abc123.js</c>.</param>
    /// <returns>The file, or <c>404</c> when there is no such file.</returns>
    public IResult ServeAsset(HttpResponse response, string assetPath)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(assetPath);

        var file = TryGetAsset(assetPath);
        if (
            file is not { PhysicalPath: { } physicalPath }
            || !AssetContentTypes.TryGetContentType(file.Name, out var contentType)
        )
        {
            return TypedResults.NotFound();
        }

        response.Headers.CacheControl = ImmutableCacheControl;

        var lastModified = file.LastModified;
        var entityTag = new EntityTagHeaderValue(
            $"\"{(lastModified.ToFileTime() ^ file.Length).ToString("x", CultureInfo.InvariantCulture)}\""
        );
        return TypedResults.PhysicalFile(
            physicalPath,
            contentType,
            lastModified: lastModified,
            entityTag: entityTag,
            enableRangeProcessing: true
        );
    }
}
