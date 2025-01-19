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
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Tokens;

/// <summary>
/// Provides the ability for the authorization server to generate security tokens.
/// </summary>
[PublicAPI]
public interface ITokenService
{
    /// <summary>
    /// Generates an access token.
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
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates a id token.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the id token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="SecurityToken"/>
    /// that represents newly generated the id token.</returns>
    ValueTask<SecurityToken> CreateIdTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates a refresh token.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="CreateSecurityTokenRequest"/> that contains the information required to generate the refresh token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="SecurityToken"/>
    /// that represents newly generated the refresh token.</returns>
    ValueTask<SecurityToken> CreateRefreshTokenAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CreateSecurityTokenRequest tokenRequest,
        CancellationToken cancellationToken);
}
