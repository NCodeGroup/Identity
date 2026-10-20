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
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPNonceService"/> abstraction over the shared
/// <see cref="IExpiringKeySet"/>, so a server-issued DPoP nonce (RFC 9449 §8) is recognized by every replica that shares
/// the set. A nonce is recorded under its tenant, so a nonce issued for one tenant is not accepted by another.
/// </summary>
internal sealed class DefaultDPoPNonceService(IExpiringKeySet keySet, TimeProvider timeProvider)
    : IDPoPNonceService
{
    private const string Purpose = "dpop_nonce";
    private const int NonceByteLength = 16;

    private static TimeSpan NonceLifetime => TimeSpan.FromMinutes(5);

    private IExpiringKeySet KeySet { get; } = keySet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public async ValueTask<string> IssueNonceAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceByteLength));
        var key = ExpiringKeys.Create(Purpose, openIdContext.Tenant.TenantId, nonce);

        // A key already present is a live nonce with this value, which validates just the same.
        await KeySet.TryAddAsync(key, TimeProvider.GetUtcNow() + NonceLifetime, cancellationToken);

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

        var key = ExpiringKeys.Create(Purpose, openIdContext.Tenant.TenantId, nonce);
        return await KeySet.ContainsAsync(key, cancellationToken);
    }
}
