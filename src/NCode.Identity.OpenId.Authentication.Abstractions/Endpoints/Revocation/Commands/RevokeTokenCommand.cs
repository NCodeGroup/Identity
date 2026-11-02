// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;

/// <summary>
/// Represents a mediator command that revokes a token on behalf of an authenticated client (RFC 7009). The matching
/// grant is invalidated; per the specification, an unknown or already-invalid token is treated as a successful no-op.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> requesting the revocation.</param>
/// <param name="TokenRevocationRequest">The <see cref="ITokenRevocationRequest"/> identifying the token to revoke.</param>
[PublicAPI]
public readonly record struct RevokeTokenCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRevocationRequest TokenRevocationRequest
) : ICommand;
