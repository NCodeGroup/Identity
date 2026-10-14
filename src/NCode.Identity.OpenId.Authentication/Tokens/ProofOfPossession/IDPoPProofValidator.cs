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

using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the ability to validate a DPoP proof JWT
/// (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-4.3">RFC 9449 §4.3</see>) — presented at the token
/// endpoint or at a protected resource — and compute the JWK thumbprint (<c>jkt</c>) the token is bound to.
/// </summary>
internal interface IDPoPProofValidator
{
    /// <summary>
    /// Validates a DPoP proof JWT against the request it accompanies, throwing an OpenID <c>invalid_dpop_proof</c> or
    /// <c>use_dpop_nonce</c> error on any violation, and returns the base64url SHA-256 JWK thumbprint of the proof key.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="request">The <see cref="DPoPProofValidationRequest"/> describing the proof and what it must bind to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The base64url-encoded SHA-256 JWK thumbprint (RFC 7638) of the proof's public key.</returns>
    ValueTask<string> ValidateAsync(
        OpenIdContext openIdContext,
        DPoPProofValidationRequest request,
        CancellationToken cancellationToken
    );
}
