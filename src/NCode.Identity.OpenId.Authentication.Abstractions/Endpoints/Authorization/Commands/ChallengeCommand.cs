// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents a mediator command that challenges the end-user to authenticate interactively (by signing in or through
/// single sign-on) when no subject could be authenticated for an authorization request. It returns a
/// <see cref="ReadOnlyEndpointDisposition"/> that typically redirects the user-agent to the configured login surface.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the authorization request.</param>
/// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> that triggered the challenge.</param>
/// <param name="AuthenticationProperties">The <see cref="AuthenticationProperties"/> carried into the challenge, such as the return URL that resumes the request.</param>
[PublicAPI]
public readonly record struct ChallengeCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IAuthorizationRequest AuthorizationRequest,
    AuthenticationProperties AuthenticationProperties
) : ICommand<ReadOnlyEndpointDisposition>;
