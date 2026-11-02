// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents a mediator command that authorizes an authenticated end-user for an authorization request, producing
/// an <see cref="AuthorizeDisposition"/> that captures the authorization outcome (such as the consent decision and
/// the data from which the authorization response is built).
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the authorization request.</param>
/// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> being authorized.</param>
/// <param name="SubjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the authenticated end-user and their session.</param>
[PublicAPI]
public readonly record struct AuthorizeCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IAuthorizationRequest AuthorizationRequest,
    SubjectAuthentication SubjectAuthentication
) : ICommand<AuthorizeDisposition>;
