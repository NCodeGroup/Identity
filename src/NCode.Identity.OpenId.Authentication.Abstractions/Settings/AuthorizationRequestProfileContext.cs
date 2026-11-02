// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The context supplied to an <see cref="IAuthorizationRequestProfileRequirement"/> while validating an authorization
/// request.
/// </summary>
/// <param name="OpenIdContext">The ambient <see cref="OpenIdContext"/> (environment, error factory, tenant, services).</param>
/// <param name="Client">The resolved client the authorization request targets.</param>
/// <param name="Request">
/// The composed authorization request (request object, PAR and query parameters resolved), so a requirement can judge
/// the requested response type and response mode.
/// </param>
/// <param name="Delivery">
/// The delivery derived for the request: its redirect URI, the handler of its effective response mode, whose traits a
/// requirement may judge, and its state.
/// </param>
[PublicAPI]
public sealed record AuthorizationRequestProfileContext(
    OpenIdContext OpenIdContext,
    OpenIdClient Client,
    IAuthorizationRequest Request,
    AuthorizationResponseDelivery Delivery
);
