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
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the ability for the authorization server to generate security tokens.
/// </summary>
[PublicAPI]
public interface ITokenService
{
    /// <summary>
    /// Generates an access token. An implementation issues it under the policy <see cref="IAccessTokenPolicyService"/>
    /// evaluates for the client and the resource servers the token targets, reading every access-token setting from
    /// <see cref="AccessTokenPolicy.Settings"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the access token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="SecurityToken"/>
    /// that represents newly generated the access token.</returns>
    ValueTask<SecurityToken> CreateAccessTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Generates an ID token. An implementation protects it with the credentials <see cref="IClientJwtCredentialSelector"/>
    /// selects for the client and <see cref="ClientJwtProtectionSettings.IdToken"/>, so the ID token is signed and
    /// encrypted exactly as the client registered.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the ID token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="SecurityToken"/>
    /// that represents the newly generated ID token.</returns>
    ValueTask<SecurityToken> CreateIdTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Generates a refresh token. A request without <see cref="CreateSecurityTokenRequest.RefreshTokenLineageId"/>
    /// begins a new rotation family at <see cref="CreateSecurityTokenRequest.CreatedWhen"/>; the token expires as the
    /// client's <c>refresh_token_expiration_policy</c> says, never after its family does (ADR-0297).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the refresh token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="IssuedRefreshToken"/> that represents the newly generated refresh token.</returns>
    ValueTask<IssuedRefreshToken> CreateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Generates a refresh token that replaces a presented one. The presented token's grant is revoked and the
    /// replacement grant is stored in the same unit of work, so the replacement token exists only if this call spent
    /// the presented token. The replacement token joins the rotation family named by
    /// <see cref="CreateSecurityTokenRequest.RefreshTokenLineageId"/>, which began at
    /// <see cref="CreateSecurityTokenRequest.RefreshTokenLineageCreatedWhen"/>, and expires no later than that family.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="presentedGrantId">The <see cref="PersistedGrantId"/> of the refresh token that the client presented and that the replacement token replaces.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the refresh token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="IssuedRefreshToken"/> that represents the replacement refresh token, or <c>null</c> when the presented
    /// token is no longer active because a concurrent request rotated or revoked it first, in which case nothing is
    /// stored or audited.</returns>
    ValueTask<IssuedRefreshToken?> RotateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        PersistedGrantId presentedGrantId,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Generates another refresh token in the rotation family named by
    /// <see cref="CreateSecurityTokenRequest.RefreshTokenLineageId"/>, for a client that presents a rotated refresh
    /// token again within its reuse interval. The token is stored only while a grant of the family is still active, and
    /// expires no later than the family.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the refresh token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="IssuedRefreshToken"/> that represents the refresh token, or <c>null</c> when no grant of the family is
    /// active, in which case nothing is stored or audited.</returns>
    /// <exception cref="ArgumentException">The request names no refresh-token rotation family.</exception>
    ValueTask<IssuedRefreshToken?> ReissueRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken
    );
}
