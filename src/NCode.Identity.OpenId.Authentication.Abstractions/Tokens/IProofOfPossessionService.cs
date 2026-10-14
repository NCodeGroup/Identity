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

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the ability to evaluate sender-constraining proof of possession for a token request — validating a DPoP
/// proof (<see href="https://datatracker.ietf.org/doc/html/rfc9449">RFC 9449</see>) and/or a mutual-TLS client
/// certificate (<see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see>) — and producing the
/// <see cref="ProofOfPossessionBinding"/> that governs the issued access token's <c>token_type</c> and <c>cnf</c> claim.
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
}
