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
/// Changes the password of the end user who is signed in to the interactive surface. This is the pluggable backend seam
/// of the self-service change-password page: the surface itself (cookie scheme, session, antiforgery, endpoints) is
/// backend-agnostic and defers the credential change to the registered implementation. The reference local-account
/// implementation is provided by the local-account authentication capability; a host whose end users have no password
/// to change, such as one fronting an external identity provider, keeps the default, which reports that no change is
/// supported.
/// </summary>
[PublicAPI]
public interface IInteractivePasswordChanger
{
    /// <summary>
    /// Changes the password of the signed-in end user after it verifies the current one. A backend without a password
    /// to change for the end user reports <see cref="InteractivePasswordChangeResult.NotSupported"/>; a backend that
    /// sets passwords judges the new one with the server's password policy and reports a refusal with
    /// <see cref="InteractivePasswordChangeResult.Refused"/> and the policy's error code.
    /// </summary>
    /// <param name="openIdContext">The resolved <see cref="OpenIdContext"/> whose tenant scopes the credential
    /// lookup.</param>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> of the signed-in end user, as the cookie session
    /// carries it.</param>
    /// <param name="currentPassword">The password the end user currently has, which proves they know it.</param>
    /// <param name="newPassword">The password the end user chose to replace it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>
    /// The <see cref="InteractivePasswordChangeResult"/> that reports the change, or the error code that refused it.
    /// </returns>
    ValueTask<InteractivePasswordChangeResult> ChangePasswordAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    );
}
