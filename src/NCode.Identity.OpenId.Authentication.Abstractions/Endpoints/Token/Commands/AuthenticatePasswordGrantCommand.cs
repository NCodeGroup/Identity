// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;

/// <summary>
/// Represents a mediator command that authenticates the resource owner for an OAuth 2.0 password grant
/// (<c>grant_type=password</c>) from the credentials in the token request, producing an
/// <see cref="AuthenticateSubjectDisposition"/>. The default handler reports the grant as unsupported; a capability
/// package replaces it to validate the supplied username and password.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> making the token request.</param>
/// <param name="TokenRequest">The <see cref="ITokenRequest"/> carrying the resource-owner credentials.</param>
/// <param name="AuthenticationProperties">The <see cref="AuthenticationProperties"/> established at the start of the
/// token-request flow (carrying the authentication's issued timestamp) that the resulting
/// <see cref="SubjectAuthentication"/> ticket must carry, so the password grant records the same authentication
/// metadata as the interactive and bearer entry points instead of a freshly-constructed empty instance.</param>
[PublicAPI]
public readonly record struct AuthenticatePasswordGrantCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRequest TokenRequest,
    AuthenticationProperties AuthenticationProperties
) : ICommand<AuthenticateSubjectDisposition>;
