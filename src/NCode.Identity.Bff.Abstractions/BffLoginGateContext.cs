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
