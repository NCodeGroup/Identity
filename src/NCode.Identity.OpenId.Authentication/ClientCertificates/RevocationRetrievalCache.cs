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

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Caches the retrieval of revocation information by key. A retrieved value is reused until the time its retrieval
/// names, concurrent callers of one key share a single retrieval, and a retrieval that yielded nothing usable is
/// remembered for <see cref="ClientCertificateRevocationOptions.FailureCacheDuration"/>.
/// </summary>
// See ADR-0252.
internal sealed class RevocationRetrievalCache(
    IMemoryCache memoryCache,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<RevocationRetrievalCache> logger
)
{
    private const string UnavailableKeySuffix = ":unavailable";

    private IMemoryCache MemoryCache { get; } = memoryCache;
    private ClientCertificateRevocationOptions Options { get; } = options.Value;
    private ILogger<RevocationRetrievalCache> Logger { get; } = logger;

    private ConcurrentDictionary<string, Lazy<Task<object?>>> Retrievals { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether a failed retrieval for <paramref name="key"/> is remembered.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <returns><see langword="true"/> when the failure is remembered; otherwise, <see langword="false"/>.</returns>
    public bool IsUnavailable(string key) =>
        MemoryCache.TryGetValue(key + UnavailableKeySuffix, out _);

    /// <summary>
    /// Remembers that a retrieval for <paramref name="key"/> yielded nothing usable, for
    /// <see cref="ClientCertificateRevocationOptions.FailureCacheDuration"/>.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    public void MarkUnavailable(string key) =>
        MemoryCache.Set(key + UnavailableKeySuffix, true, Options.FailureCacheDuration);

    /// <summary>
    /// Gets the cached value for <paramref name="key"/>, or runs <paramref name="retrieve"/> to obtain it.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <param name="location">The location that the log entries name.</param>
    /// <param name="subject">The subject of the certificate that the log entries name.</param>
    /// <param name="retrieve">The retrieval to run when no value is cached and no failure is remembered.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to stop waiting for the retrieval.</param>
    /// <typeparam name="T">The type of the retrieved value.</typeparam>
    /// <returns>The value, or <see langword="null"/> when nothing usable is available.</returns>
    public async ValueTask<T?> GetAsync<T>(
        string key,
        string location,
        string subject,
        Func<Task<RevocationRetrieval<T>>> retrieve,
        CancellationToken cancellationToken
    )
        where T : class
    {
        if (MemoryCache.TryGetValue<T>(key, out var cached) && cached is not null)
            return cached;

        // A retrieval that yielded nothing usable is remembered, so an unreachable or misbehaving source is not
        // asked again for every check. See ADR-0252.
        if (IsUnavailable(key))
        {
            Logger.RevocationRetrievalSuppressed(location, subject, Options.FailureCacheDuration);
            return null;
        }

        // Concurrent checks share one retrieval; it runs without the caller's token, so a caller that gives up does
        // not break it for the others. See ADR-0252.
        var retrieval = Retrievals.GetOrAdd(
            key,
            _ => new Lazy<Task<object?>>(() => RunAsync(key, retrieve))
        );
        return await retrieval.Value.WaitAsync(cancellationToken) as T;
    }

    // A Task, not a ValueTask, because every caller sharing the retrieval awaits it.
    private async Task<object?> RunAsync<T>(string key, Func<Task<RevocationRetrieval<T>>> retrieve)
        where T : class
    {
        try
        {
            var result = await retrieve();
            if (result.Value is null)
            {
                MarkUnavailable(key);
                return null;
            }

            if (result.CacheUntil is { } cacheUntil)
                MemoryCache.Set(key, result.Value, cacheUntil);

            return result.Value;
        }
        finally
        {
            Retrievals.TryRemove(key, out _);
        }
    }
}
