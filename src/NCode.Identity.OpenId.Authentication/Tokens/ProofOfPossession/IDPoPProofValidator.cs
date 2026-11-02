// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
