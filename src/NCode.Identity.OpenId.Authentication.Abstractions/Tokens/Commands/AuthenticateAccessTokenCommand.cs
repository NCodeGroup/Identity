// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Commands;

/// <summary>
/// Represents a mediator command that authenticates a self-issued JWT access token presented on the current request,
/// validating it against the resolved tenant's signing keys, issuer, lifetime, and the required audience, and returning
/// an <see cref="AuthenticateAccessTokenDisposition"/> carrying the authenticated principal.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> for the current request, supplying the tenant whose keys
/// and issuer validate the token.</param>
/// <param name="AccessToken">The encoded JWT access token to validate.</param>
/// <param name="RequiredAudiences">The audience values the token must contain; an empty collection skips audience
/// validation.</param>
[PublicAPI]
public readonly record struct AuthenticateAccessTokenCommand(
    OpenIdContext OpenIdContext,
    string AccessToken,
    IReadOnlyCollection<string> RequiredAudiences
) : ICommand<AuthenticateAccessTokenDisposition>;
