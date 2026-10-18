#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using Microsoft.Extensions.Caching.Distributed;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPProofReplayCache"/> abstraction over
/// <see cref="IDistributedCache"/>, so DPoP proof replay (RFC 9449 §11.1) is detected across every node of a
/// multi-instance deployment. The server registers an in-memory <see cref="IDistributedCache"/> by default (single
/// node); a cluster swaps in a shared provider (for example Redis) by registering it in DI.
/// </summary>
internal sealed class DefaultDPoPProofReplayCache(IDistributedCache distributedCache)
    : IDPoPProofReplayCache
{
    private static ReadOnlySpan<byte> Marker => [1];

    private IDistributedCache DistributedCache { get; } = distributedCache;

    /// <inheritdoc />
    public async ValueTask<bool> TryRecordAsync(
        string jwkThumbprint,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var key = $"dpop_jti:{jwkThumbprint}:{jti}";

        // IDistributedCache has no atomic add-if-absent, so two concurrent identical proofs could both observe an
        // empty slot; the window is bounded by the proof's short acceptance lifetime and is acceptable for replay
        // detection. A provider-specific atomic implementation may replace this seam when stricter guarantees matter.
        var existing = await DistributedCache.GetAsync(key, cancellationToken);
        if (existing is not null)
            return false;

        await DistributedCache.SetAsync(
            key,
            Marker.ToArray(),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresWhen },
            cancellationToken
        );
        return true;
    }
}
