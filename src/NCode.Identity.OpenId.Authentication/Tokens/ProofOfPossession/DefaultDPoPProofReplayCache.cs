// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPProofReplayCache"/> abstraction over the shared
/// <see cref="IExpiringKeySet"/>, so DPoP proof replay (RFC 9449 §11.1) is detected by every replica that shares the set.
/// The set's atomic add-if-absent lets exactly one of any number of concurrent presentations of a proof succeed.
/// </summary>
internal sealed class DefaultDPoPProofReplayCache(IExpiringKeySet keySet) : IDPoPProofReplayCache
{
    private const string Purpose = "dpop_jti";

    private IExpiringKeySet KeySet { get; } = keySet;

    /// <inheritdoc />
    public ValueTask<bool> TryRecordAsync(
        OpenIdContext openIdContext,
        string jwkThumbprint,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var key = ExpiringKeys.Create(Purpose, openIdContext.Tenant.TenantId, jwkThumbprint, jti);
        return KeySet.TryAddAsync(key, expiresWhen, cancellationToken);
    }
}
