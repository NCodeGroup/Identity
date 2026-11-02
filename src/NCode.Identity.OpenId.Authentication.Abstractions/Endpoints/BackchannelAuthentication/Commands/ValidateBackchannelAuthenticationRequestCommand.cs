// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;

/// <summary>
/// Represents a mediator command that validates a CIBA backchannel authentication request against the protocol rules
/// and the client's configuration. A handler reports a validation failure as an OpenID error; a successful validation
/// completes without a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> that initiated the backchannel authentication request.</param>
/// <param name="Request">The <see cref="IBackchannelAuthenticationRequest"/> being validated.</param>
[PublicAPI]
public readonly record struct ValidateBackchannelAuthenticationRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IBackchannelAuthenticationRequest Request
) : ICommand;
