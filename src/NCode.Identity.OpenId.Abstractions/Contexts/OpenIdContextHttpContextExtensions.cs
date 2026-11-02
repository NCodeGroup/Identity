// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides extension methods to retrieve the ambient <see cref="OpenIdContext"/> that the OpenID pipeline publishes on
/// the <see cref="HttpContext"/> at request entry.
/// </summary>
[PublicAPI]
public static class OpenIdContextHttpContextExtensions
{
    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> for the current request, or <c>null</c> when the request has not entered
    /// the OpenID pipeline.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The <see cref="OpenIdContext"/>, or <c>null</c> when it is not available.</returns>
    public static OpenIdContext? GetOpenIdContextOrDefault(this HttpContext httpContext) =>
        httpContext.Features.Get<IOpenIdContextFeature>()?.OpenIdContext;

    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> for the current request, throwing when it is not available.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The <see cref="OpenIdContext"/> for the current request.</returns>
    /// <exception cref="InvalidOperationException">The request has not entered the OpenID pipeline.</exception>
    public static OpenIdContext GetOpenIdContext(this HttpContext httpContext) =>
        httpContext.GetOpenIdContextOrDefault()
        ?? throw new InvalidOperationException(
            "The OpenID request environment is not available; this operation must run within an OpenID endpoint pipeline."
        );
}
