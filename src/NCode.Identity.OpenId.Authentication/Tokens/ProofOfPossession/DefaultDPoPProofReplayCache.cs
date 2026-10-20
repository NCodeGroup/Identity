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
