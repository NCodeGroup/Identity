#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
