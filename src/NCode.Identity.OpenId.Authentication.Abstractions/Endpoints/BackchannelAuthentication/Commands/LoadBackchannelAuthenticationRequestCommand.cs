// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;

/// <summary>
/// Represents a mediator command to load the effective CIBA backchannel authentication request. When the client sends
/// a signed (and optionally encrypted) <c>request</c> JWT, the default handler validates it and uses its claims as the
/// authoritative request parameters (OpenID Connect CIBA §7.1.1); otherwise the form-based request is used as-is.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the backchannel authentication request.</param>
/// <param name="Request">The form-based <see cref="IBackchannelAuthenticationRequest"/> to resolve into the effective request.</param>
[PublicAPI]
public readonly record struct LoadBackchannelAuthenticationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IBackchannelAuthenticationRequest Request
) : ICommand<IBackchannelAuthenticationRequest>;
