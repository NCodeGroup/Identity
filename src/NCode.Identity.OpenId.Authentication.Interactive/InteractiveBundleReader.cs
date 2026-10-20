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

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Reads the single-page bundle's <c>index.html</c> so the login, consent, and error pages can inject their
/// server-rendered form into the bundle's <c>#root</c> mount point (progressive enhancement). The index is read once
/// and cached; each request compares the file's last-write time and length with the cached copy's, so a locally
/// rebuilt bundle is picked up without a restart. The no-JavaScript forms render standalone when no bundle is
/// configured or available.
/// </summary>
// The cache is revalidated from file metadata rather than invalidated by a file watcher: a watcher costs an inotify
// instance per process and misses changes on bind-mounted and network file systems, while one metadata read per
// request is far cheaper than the file read it replaces.
internal sealed class InteractiveBundleReader(
    IOptions<InteractiveAuthenticationOptions> optionsAccessor
)
{
    private const string IndexFileName = "index.html";

    private string? IndexPath { get; } =
        string.IsNullOrEmpty(optionsAccessor.Value.BundleRootPath)
            ? null
            : Path.Combine(optionsAccessor.Value.BundleRootPath, IndexFileName);

    // Replaced as a whole, so a request sees either the previous or the new version, never a mix of the two.
    private InteractiveBundleIndex? CachedIndex { get; set; }

    /// <summary>
    /// Reads the single-page bundle's <c>index.html</c>, or returns <see langword="null"/> when no bundle is
    /// configured or available.
    /// </summary>
    public async ValueTask<string?> TryReadIndexHtmlAsync(CancellationToken cancellationToken) =>
        (await TryReadIndexAsync(cancellationToken))?.Html;

    /// <summary>
    /// Reads the single-page bundle's <c>index.html</c> split at its <c>#root</c> mount point, or returns
    /// <see langword="null"/> when no bundle is configured or available, or the bundle has no empty mount point.
    /// </summary>
    public async ValueTask<InteractiveBundleShell?> TryReadShellAsync(
        CancellationToken cancellationToken
    ) => (await TryReadIndexAsync(cancellationToken))?.Shell;

    private async ValueTask<InteractiveBundleIndex?> TryReadIndexAsync(
        CancellationToken cancellationToken
    )
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
            return cachedIndex;
        }

        // A rebuild that lands between the metadata read and this read is cached under the older metadata, so the
        // next request sees a mismatch and reads the file again.
        // The document is split once per version, so each page render writes its slices rather than searching it.
        var html = await File.ReadAllTextAsync(IndexPath, cancellationToken);
        var index = new InteractiveBundleIndex(
            lastWriteTimeUtc,
            length,
            html,
            InteractiveBundleShell.TryCreate(html)
        );
        CachedIndex = index;
        return index;
    }
}
