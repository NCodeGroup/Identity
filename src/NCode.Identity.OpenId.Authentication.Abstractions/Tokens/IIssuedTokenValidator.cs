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
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Validates a JSON Web Token (JWT) that the tenant of the current request issued, under the one policy for its kind.
/// Every reader of a token the server issued — the protected resources, introspection, token exchange, and the
/// end-session endpoint — validates it through this service, so the policy is stated once.
/// </summary>
/// <remarks>
/// <para>
/// An access token is a signed JWT whose <c>typ</c> is <c>at+jwt</c>, issued by the tenant and within its lifetime,
/// signed with an algorithm the tenant's <c>access_token_signing_alg_values_supported</c> permits and verified with the
/// tenant's keys. An encrypted access token is a Nested JWT whose encryption the tenant's
/// <c>access_token_encryption_alg_values_supported</c> and <c>access_token_encryption_enc_values_supported</c> permit,
/// decrypted with the tenant's keys and any keys the reader adds; a compressed token is never accepted. An access token
/// issued under an end-user session that has been revoked is not valid.
/// </para>
/// <para>
/// An ID token is a signed JWT whose <c>typ</c> is <c>JWT</c>, issued by the tenant and signed with an algorithm the
/// tenant's <c>id_token_signing_alg_values_supported</c> permits. An ID token is encrypted only to its client, so the
/// server never decrypts one.
/// </para>
/// <para>
/// Register a custom implementation to extend the policy. A replacement must not weaken the checks of the default
/// implementation.
/// </para>
/// </remarks>
// See ADR-0167.
[PublicAPI]
public interface IIssuedTokenValidator
{
    /// <summary>
    /// Validates an access token the tenant issued.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> whose tenant issued the token.</param>
    /// <param name="accessToken">The encoded access token.</param>
    /// <param name="options">The audiences the reader requires and the keys it adds for decryption.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="JwtValidationResult"/>; a token that cannot be decoded or fails the policy is an invalid result, never
    /// an exception.</returns>
    ValueTask<JwtValidationResult> ValidateAccessTokenAsync(
        OpenIdContext openIdContext,
        string accessToken,
        AccessTokenValidationOptions options,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates an ID token the tenant issued, including its lifetime.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> whose tenant issued the token.</param>
    /// <param name="idToken">The encoded ID token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="JwtValidationResult"/>; a token that cannot be decoded or fails the policy is an invalid result, never
    /// an exception.</returns>
    ValueTask<JwtValidationResult> ValidateIdTokenAsync(
        OpenIdContext openIdContext,
        string idToken,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates an ID token the tenant issued that a client presents as a hint to the end-user's session, as the
    /// <c>id_token_hint</c> of an end-session request. A hint identifies a session that may already have outlived the
    /// token, so its lifetime is not checked; every other check of an ID token is.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> whose tenant issued the token.</param>
    /// <param name="idTokenHint">The encoded ID token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="JwtValidationResult"/>; a token that cannot be decoded or fails the policy is an invalid result, never
    /// an exception.</returns>
    ValueTask<JwtValidationResult> ValidateIdTokenHintAsync(
        OpenIdContext openIdContext,
        string idTokenHint,
        CancellationToken cancellationToken
    );
}
