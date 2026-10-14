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

using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPNonceService"/> abstraction over
/// <see cref="IDistributedCache"/>, so a server-issued DPoP nonce (RFC 9449 §8) is recognized across every node of a
/// multi-instance deployment. The server registers an in-memory <see cref="IDistributedCache"/> by default (single
/// node); a cluster swaps in a shared provider (for example Redis) by registering it in DI.
/// </summary>
internal sealed class DefaultDPoPNonceService(IDistributedCache distributedCache)
    : IDPoPNonceService
{
    private const int NonceByteLength = 16;

    private static ReadOnlySpan<byte> Marker => [1];
    private static TimeSpan NonceLifetime => TimeSpan.FromMinutes(5);

    private IDistributedCache DistributedCache { get; } = distributedCache;

    /// <inheritdoc />
    public async ValueTask<string> IssueNonceAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceByteLength));

        await DistributedCache.SetAsync(
            $"dpop_nonce:{nonce}",
            Marker.ToArray(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = NonceLifetime },
            cancellationToken
        );

        return nonce;
    }

    /// <inheritdoc />
    public async ValueTask<bool> ValidateNonceAsync(
        OpenIdContext openIdContext,
        string nonce,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(nonce))
            return false;

        var value = await DistributedCache.GetAsync($"dpop_nonce:{nonce}", cancellationToken);
        return value is not null;
    }
}
