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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides single-use (replay) detection for JWT client-authentication assertions (<c>private_key_jwt</c> and
/// <c>client_secret_jwt</c>) keyed by their <c>jti</c> and the authenticating client, as required by
/// <see href="https://datatracker.ietf.org/doc/html/rfc7523#section-3">RFC 7523 §3</see> and OpenID Connect Core §9.
/// The default implementation is backed by <c>IDistributedCache</c> (in-memory and therefore per-instance by default);
/// a multi-instance deployment should register a distributed implementation so an assertion cannot be replayed against
/// another node within its acceptance window.
/// </summary>
[PublicAPI]
public interface IClientAssertionReplayCache
{
    /// <summary>
    /// Atomically records a client-authentication assertion as seen, returning whether it was previously unseen. A
    /// return of <see langword="false"/> indicates a replay and the assertion must be rejected.
    /// </summary>
    /// <param name="clientId">The identifier of the authenticating client, namespacing the <paramref name="jti"/>.</param>
    /// <param name="jti">The <c>jti</c> (unique identifier) claim of the assertion.</param>
    /// <param name="expiresWhen">The instant after which the record may be evicted (the assertion's <c>exp</c>, extended by clock skew).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the assertion was newly recorded; <see langword="false"/> if it was already seen (a replay).</returns>
    ValueTask<bool> TryRecordAsync(
        string clientId,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );
}
