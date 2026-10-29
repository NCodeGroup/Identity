#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
