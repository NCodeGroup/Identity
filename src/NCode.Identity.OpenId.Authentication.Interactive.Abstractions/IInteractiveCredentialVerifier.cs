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
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Verifies the credentials submitted to the interactive login surface and, on success, produces the
/// <see cref="ClaimsPrincipal"/> the cookie session is issued for. This is the pluggable backend seam of the
/// interactive surface: the surface itself (cookie scheme, pages, antiforgery, endpoints) is backend-agnostic and
/// defers the actual authentication to the registered implementation. The reference local-account implementation is
/// provided by the local-account authentication capability; a host fronting LDAP, an external identity provider, or a
/// passwordless backend registers its own implementation instead.
/// </summary>
[PublicAPI]
public interface IInteractiveCredentialVerifier
{
    /// <summary>
    /// Verifies the submitted username/password within the resolved OpenID request environment and, on success,
    /// builds the <see cref="ClaimsPrincipal"/> carrying the subject and issuer claims the authorize flow reads.
    /// </summary>
    /// <param name="openIdContext">The resolved <see cref="OpenIdContext"/> whose tenant scopes the credential lookup
    /// and supplies the subject/issuer claim configuration.</param>
    /// <param name="username">The submitted username.</param>
    /// <param name="password">The submitted password.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The authenticated <see cref="ClaimsPrincipal"/> when the credentials are valid; otherwise
    /// <see langword="null"/>.</returns>
    ValueTask<ClaimsPrincipal?> VerifyCredentialAsync(
        OpenIdContext openIdContext,
        string username,
        string password,
        CancellationToken cancellationToken
    );
}
