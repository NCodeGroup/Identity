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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;

/// <summary>
/// Decides whether a refresh token must be rotated, that is replaced by a new refresh token when it is used. Under
/// the <c>always</c> policy every refresh token is rotated. Under the <c>unbound</c> policy a refresh token is rotated
/// unless it is bound to a proof-of-possession key or to client authentication: a public client's refresh token
/// without a DPoP or certificate binding is always rotated, as RFC 9700 requires, and no setting turns that off.
/// </summary>
// See ADR-0216.
internal static class RefreshTokenRotationRequirement
{
    /// <summary>
    /// Gets a value indicating whether the presented refresh token must be replaced by a new refresh token.
    /// </summary>
    /// <param name="openIdClient">The client redeeming the refresh token, as the token request authenticated it.</param>
    /// <param name="confirmation">The proof-of-possession key the presented refresh token is bound to.</param>
    /// <returns><see langword="true"/> when the refresh token must be rotated.</returns>
    public static bool IsRequired(OpenIdClient openIdClient, TokenConfirmation confirmation)
    {
        var policy = openIdClient.Settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy);
        if (
            string.Equals(
                policy,
                OpenIdConstants.RefreshTokenRotationPolicy.Always,
                StringComparison.Ordinal
            )
        )
            return true;

        return !openIdClient.IsConfidential && !confirmation.HasValue;
    }
}
