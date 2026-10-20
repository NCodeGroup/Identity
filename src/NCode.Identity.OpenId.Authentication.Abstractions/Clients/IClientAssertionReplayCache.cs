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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides single-use (replay) detection for JWT client-authentication assertions (<c>private_key_jwt</c> and
/// <c>client_secret_jwt</c>) keyed by the tenant, the authenticating client, and their <c>jti</c>, as required by
/// <see href="https://datatracker.ietf.org/doc/html/rfc7523#section-3">RFC 7523 §3</see> and OpenID Connect Core §9.
/// The default implementation records each assertion in the shared <c>IExpiringKeySet</c>, so an assertion replayed
/// against any replica that shares the set is rejected; a deployment that runs several replicas registers a shared
/// adapter for that set.
/// </summary>
[PublicAPI]
public interface IClientAssertionReplayCache
{
    /// <summary>
    /// Atomically records a client-authentication assertion as seen, returning whether it was previously unseen. A
    /// return of <see langword="false"/> indicates a replay and the assertion must be rejected.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request, whose tenant namespaces the record.</param>
    /// <param name="clientId">The identifier of the authenticating client, namespacing the <paramref name="jti"/>.</param>
    /// <param name="jti">The <c>jti</c> (unique identifier) claim of the assertion.</param>
    /// <param name="expiresWhen">The instant after which the record may be evicted (the assertion's <c>exp</c>, extended by clock skew).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the assertion was newly recorded; <see langword="false"/> if it was already seen (a
    /// replay) or can no longer be accepted.
    /// </returns>
    ValueTask<bool> TryRecordAsync(
        OpenIdContext openIdContext,
        string clientId,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );
}
