// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the ability for the authorization server to generate authorization codes in response to authorization requests.
/// </summary>
[PublicAPI]
public interface IAuthorizationCodeService
{
    /// <summary>
    /// Generates the authorization code for an authorization request.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="authorizationRequest">The <see cref="IAuthorizationRequest"/> that represents the authorization request.</param>
    /// <param name="subjectAuthentication">The <see cref="SubjectAuthentication"/> that represents the authenticated user identity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="SecurityToken"/> newly generated authorization code.</returns>
    ValueTask<SecurityToken> CreateAuthorizationCodeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    );
}
