// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Carries what an <see cref="IBffLoginGate"/> needs to decide whether a validated login may create a session.
/// </summary>
/// <param name="httpContext">The HTTP context of the login callback.</param>
/// <param name="issuer">The issuer of the provider that authenticated the user.</param>
/// <param name="principal">The principal built from the claims of the validated ID token.</param>
[PublicAPI]
public sealed class BffLoginGateContext(
    HttpContext httpContext,
    string issuer,
    ClaimsPrincipal principal
)
{
    /// <summary>
    /// Gets the HTTP context of the login callback.
    /// </summary>
    public HttpContext HttpContext { get; } =
        httpContext ?? throw new ArgumentNullException(nameof(httpContext));

    /// <summary>
    /// Gets the issuer of the provider that authenticated the user.
    /// </summary>
    public string Issuer { get; } = issuer ?? throw new ArgumentNullException(nameof(issuer));

    /// <summary>
    /// Gets the principal built from the claims of the validated ID token.
    /// </summary>
    public ClaimsPrincipal Principal { get; } =
        principal ?? throw new ArgumentNullException(nameof(principal));
}
