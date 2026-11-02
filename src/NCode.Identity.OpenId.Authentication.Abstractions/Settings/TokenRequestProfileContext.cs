// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The context supplied to an <see cref="ITokenRequestProfileRequirement"/> while evaluating a token request.
/// </summary>
/// <param name="OpenIdContext">The ambient <see cref="OpenIdContext"/> (environment, error factory, tenant, services).</param>
/// <param name="Client">The authenticated client making the token request.</param>
/// <param name="Confirmation">The sender-constraining confirmation computed for the request (DPoP / mutual-TLS), if any.</param>
[PublicAPI]
public sealed record TokenRequestProfileContext(
    OpenIdContext OpenIdContext,
    OpenIdClient Client,
    TokenConfirmation Confirmation
);
