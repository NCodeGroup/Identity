// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.ManagementConsole.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Reads the console bundle's <c>index.html</c> and serves the files of its <c>assets</c> directory. The index is read
/// once and cached; each request compares the file's last-write time and length with the cached copy's, so a rebuilt
/// bundle is picked up without a restart.
/// </summary>
// The cache is revalidated from file metadata rather than invalidated by a file watcher: a watcher costs an inotify
// instance per process and misses changes on bind-mounted and network file systems, while one metadata read per
// request is far cheaper than the file read it replaces.
internal sealed class ManagementConsoleBundleReader(
    IOptions<ManagementConsoleOptions> optionsAccessor,
    IHostEnvironment environment
) : IDisposable
{
    private const string IndexFileName = "index.html";

    /// <summary>
    /// The directory beneath the bundle's root that holds its content-hashed files, which are the only files the host
    /// serves besides the index. It is <see cref="ManagementConsoleDefaults.AssetsPath"/> without its leading
    /// <c>/</c>, so the directory and the path under which the console serves it cannot disagree. It is a static
    /// property rather than a constant because a constant cannot be derived from another constant's value by a string
    /// method.
    /// </summary>
    public static string AssetsDirectory { get; } =
        ManagementConsoleDefaults.AssetsPath.TrimStart('/');

    /// <summary>
    /// Replaces every nonce placeholder of the bundle's <c>index.html</c> with a nonce.
    /// </summary>
    /// <param name="html">The text of the bundle's <c>index.html</c>.</param>
    /// <param name="nonce">The nonce of the response's Content-Security-Policy.</param>
    /// <returns>The page that the response writes.</returns>
    public static string RenderIndex(string html, string nonce) =>
        html.Replace(ManagementConsoleDefaults.NoncePlaceholder, nonce, StringComparison.Ordinal);

    private string? RootPath { get; } =
        string.IsNullOrWhiteSpace(optionsAccessor.Value.BundleRootPath)
            ? null
            : Path.GetFullPath(optionsAccessor.Value.BundleRootPath, environment.ContentRootPath);

    private string? AssetsRootPath =>
        RootPath is null ? null : Path.Combine(RootPath, AssetsDirectory);

    // Rooted at the bundle's assets directory, so a requested asset path that navigates above it, or names a hidden or
    // system file, resolves to no file. Created once the directory exists, which a development build may create after
    // the host starts.
    private PhysicalFileProvider? _assetProvider;

    // Replaced as a whole, so a request sees either the previous or the new version, never a mix of the two.
    private ManagementConsoleBundleIndex? CachedIndex { get; set; }

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
    /// Reads the bundle's <c>index.html</c>.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The file's text, or <see langword="null"/> when no bundle is configured or available.</returns>
    public async ValueTask<string?> TryReadIndexAsync(CancellationToken cancellationToken)
    {
        if (RootPath is null)
        {
            return null;
        }

        var indexPath = Path.Combine(RootPath, IndexFileName);
        var file = new FileInfo(indexPath);
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
            return cachedIndex.Html;
        }

        // A rebuild that lands between the metadata read and this read is cached under the older metadata, so the
        // next request sees a mismatch and reads the file again.
        var html = await File.ReadAllTextAsync(indexPath, SecureEncoding.UTF8, cancellationToken);
        CachedIndex = new ManagementConsoleBundleIndex(lastWriteTimeUtc, length, html);
        return html;
    }
}
