// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;

/// <summary>
/// Represents a mediator command that validates a token request against the protocol rules and the client's
/// configuration, before its grant is processed. A handler reports a validation failure as an OpenID error; a
/// successful validation completes without a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> making the token request.</param>
/// <param name="TokenRequest">The <see cref="ITokenRequest"/> being validated.</param>
[PublicAPI]
public readonly record struct ValidateTokenRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRequest TokenRequest
) : ICommand;
