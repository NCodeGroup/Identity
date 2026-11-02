// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;

/// <summary>
/// Represents a mediator command that loads the effective <see cref="IAuthorizationRequest"/> from the raw request
/// values, merging the direct request parameters with any <c>request</c> or <c>request_uri</c> request object so that
/// downstream handlers see a single authoritative request.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the authorization request.</param>
/// <param name="RequestValues">The raw <see cref="IRequestValues"/> parsed from the HTTP request.</param>
[PublicAPI]
public readonly record struct LoadAuthorizationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IRequestValues RequestValues
) : ICommand<IAuthorizationRequest>;
