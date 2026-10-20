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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides single-use (replay) detection for DPoP proof JWTs keyed by the tenant, the proof's bound key thumbprint,
/// and its <c>jti</c> (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-11.1">RFC 9449 §11.1</see>).
/// The default implementation records each proof in the shared <c>IExpiringKeySet</c>, so a proof replayed against
/// any replica that shares the set is rejected; a deployment that runs several replicas registers a shared adapter for
/// that set.
/// </summary>
[PublicAPI]
public interface IDPoPProofReplayCache
{
    /// <summary>
    /// Atomically records a DPoP proof as seen, returning whether it was previously unseen. A return of
    /// <see langword="false"/> indicates a replay and the proof must be rejected.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request, whose tenant namespaces the record.</param>
    /// <param name="jwkThumbprint">The base64url SHA-256 JWK thumbprint of the proof's key, namespacing the <paramref name="jti"/>.</param>
    /// <param name="jti">The <c>jti</c> (unique identifier) claim of the DPoP proof.</param>
    /// <param name="expiresWhen">The instant after which the record may be evicted (the end of the proof's acceptance window).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the proof was newly recorded; <see langword="false"/> if it was already seen (a replay)
    /// or its acceptance window has already ended.
    /// </returns>
    ValueTask<bool> TryRecordAsync(
        OpenIdContext openIdContext,
        string jwkThumbprint,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );
}
