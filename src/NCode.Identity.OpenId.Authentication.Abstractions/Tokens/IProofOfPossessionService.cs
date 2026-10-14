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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the ability to evaluate sender-constraining proof of possession — validating a DPoP proof
/// (<see href="https://datatracker.ietf.org/doc/html/rfc9449">RFC 9449</see>) and/or a mutual-TLS client certificate
/// (<see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see>) — both when issuing a token and when a
/// bound token is presented to a protected resource.
/// </summary>
[PublicAPI]
public interface IProofOfPossessionService
{
    /// <summary>
    /// Evaluates proof of possession for a token request. Reads any DPoP proof and client certificate from the current
    /// request, enforces the client's <c>require_dpop</c> and <c>tls_client_certificate_bound_access_tokens</c>
    /// policies, and returns the resulting binding. Throws an OpenID error (for example <c>invalid_dpop_proof</c> or
    /// <c>use_dpop_nonce</c>) when a presented proof is invalid or a required proof is missing.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The authenticated <see cref="OpenIdClient"/> making the token request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ProofOfPossessionBinding"/> describing the token type and confirmation to issue.</returns>
    ValueTask<ProofOfPossessionBinding> EvaluateTokenRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Verifies that the current request legitimately presents a sender-constrained access token: a DPoP-bound token
    /// (<c>cnf.jkt</c>) requires a valid DPoP proof whose key, <c>htm</c>/<c>htu</c>, and <c>ath</c> match
    /// (RFC 9449 §7.1); a certificate-bound token (<c>cnf.x5t#S256</c>) requires the matching client certificate
    /// (RFC 8705 §3). Returns the <see cref="IOpenIdError"/> describing the failure, or <see langword="null"/> when the
    /// presentation is valid (including for an unbound bearer token).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="accessToken">The access token being presented (used to verify the proof's <c>ath</c> claim).</param>
    /// <param name="confirmation">The <see cref="TokenConfirmation"/> read from the access token's <c>cnf</c> claim.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IOpenIdError"/> when the proof of possession is missing or invalid; otherwise <see langword="null"/>.</returns>
    ValueTask<IOpenIdError?> ValidateResourceAccessAsync(
        OpenIdContext openIdContext,
        string accessToken,
        TokenConfirmation confirmation,
        CancellationToken cancellationToken
    );
}
