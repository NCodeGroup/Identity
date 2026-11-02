// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace NCode.Identity.OpenId.Client.ProofOfPossession;

/// <summary>
/// Caches the in-process DPoP nonces per origin (RFC 9449 §8). They are not shared across replicas, because a stale
/// nonce costs one retry.
/// </summary>
internal sealed class DPoPNonceCache
{
    /// <summary>
    /// The maximum number of origins the cache holds; the cache is cleared before a new origin would exceed it.
    /// </summary>
    public const int MaxOrigins = 1024;

    private ConcurrentDictionary<string, string> Nonces { get; } = new(StringComparer.Ordinal);

    public string? Get(Uri requestUri) =>
        Nonces.TryGetValue(requestUri.GetLeftPart(UriPartial.Authority), out var nonce)
            ? nonce
            : null;

    public void Set(Uri requestUri, string nonce)
    {
        var origin = requestUri.GetLeftPart(UriPartial.Authority);
        if (Nonces.Count >= MaxOrigins && !Nonces.ContainsKey(origin))
            Nonces.Clear();

        Nonces[origin] = nonce;
    }
}
