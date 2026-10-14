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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the ability to validate a DPoP proof JWT presented at the token endpoint
/// (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-4.3">RFC 9449 §4.3</see>) and compute the JWK
/// thumbprint (<c>jkt</c>) the issued access token is bound to.
/// </summary>
internal interface IDPoPProofValidator
{
    /// <summary>
    /// Validates a DPoP proof JWT against the request it accompanies, throwing an OpenID <c>invalid_dpop_proof</c> or
    /// <c>use_dpop_nonce</c> error on any violation, and returns the base64url SHA-256 JWK thumbprint of the proof key.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The authenticated <see cref="OpenIdClient"/> making the request.</param>
    /// <param name="proofJwt">The compact DPoP proof JWT from the <c>DPoP</c> request header.</param>
    /// <param name="httpMethod">The HTTP method of the request the proof is bound to (the <c>htm</c> claim).</param>
    /// <param name="httpUri">The HTTP target URI of the request the proof is bound to (the <c>htu</c> claim), without query or fragment.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The base64url-encoded SHA-256 JWK thumbprint (RFC 7638) of the proof's public key.</returns>
    ValueTask<string> ValidateAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string proofJwt,
        string httpMethod,
        string httpUri,
        CancellationToken cancellationToken
    );
}
