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
/// Represents a mediator command that authenticates the end-user for an authorization request by resolving their
/// existing login (for example, from the session cookie) into an <see cref="AuthenticateSubjectDisposition"/>. When no
/// subject can be authenticated, the disposition indicates that an interactive challenge is required.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the authorization request.</param>
/// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> the end-user is being authenticated for.</param>
[PublicAPI]
public readonly record struct AuthenticateCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IAuthorizationRequest AuthorizationRequest
) : ICommand<AuthenticateSubjectDisposition>;
