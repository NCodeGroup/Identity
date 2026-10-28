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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Decides whether an authorization code must be bound to a PKCE code challenge (RFC 7636). A code is always bound,
/// except where all of the conditions of OAuth 2.1 §7.5.1.1 hold: the deployment has relaxed <c>require_pkce</c>; the
/// client is confidential and attested by <c>pkce_nonce_exemption</c> to implement the OpenID Connect nonce mechanism
/// correctly; and the request uses that mechanism — it requests the <c>openid</c> scope and carries a <c>nonce</c>.
/// A public client is therefore always bound, and so is any request that relies on neither mechanism.
/// </summary>
// See ADR-0144.
internal static class CodeChallengeRequirement
{
    /// <summary>
    /// Gets a value indicating whether an authorization request must carry a <c>code_challenge</c>. The client type is
    /// the type the client is registered as, because the authorization endpoint does not authenticate it.
    /// </summary>
    /// <param name="openIdClient">The client making the authorization request.</param>
    /// <param name="request">The authorization request.</param>
    /// <returns><see langword="true"/> when the request must carry a <c>code_challenge</c>.</returns>
    public static bool IsRequiredForAuthorization(
        OpenIdClient openIdClient,
        IAuthorizationRequest request
    ) =>
        IsRequired(
            openIdClient.Settings,
            openIdClient.IsRegisteredAsConfidential,
            request.Scopes,
            request.Nonce
        );

    /// <summary>
    /// Gets a value indicating whether redeeming an authorization code requires that the code be bound to a
    /// <c>code_challenge</c>. The client type is the one the token request authenticated, so a client that redeems
    /// without authenticating can redeem only a bound code; and the scopes are the ones the token request is
    /// effectively for, so a request that drops <c>openid</c> — and with it the ID Token that carries the nonce — can
    /// redeem only a bound code.
    /// </summary>
    /// <param name="openIdClient">The client redeeming the authorization code.</param>
    /// <param name="request">The authorization request the code was issued for.</param>
    /// <param name="effectiveScopes">The scopes the token request is effectively for.</param>
    /// <returns><see langword="true"/> when only a code bound to a <c>code_challenge</c> may be redeemed.</returns>
    public static bool IsRequiredForRedemption(
        OpenIdClient openIdClient,
        IAuthorizationRequest request,
        IReadOnlyCollection<string> effectiveScopes
    ) =>
        IsRequired(
            openIdClient.Settings,
            openIdClient.IsConfidential,
            effectiveScopes,
            request.Nonce
        );

    private static bool IsRequired(
        IReadOnlySettingCollection settings,
        bool isConfidentialClient,
        IReadOnlyCollection<string> scopes,
        string? nonce
    )
    {
        if (settings.GetValue(OpenIdSettingKeys.RequireCodeChallenge) || !isConfidentialClient)
            return true;

        if (!settings.GetValue(OpenIdSettingKeys.PkceNonceExemption))
            return true;

        // The nonce mitigates code injection only in an OpenID Connect request that uses it.
        var usesNonce =
            !string.IsNullOrEmpty(nonce)
            && scopes.Contains(OpenIdProtocol.ScopeTypes.OpenId, StringComparer.Ordinal);

        return !usesNonce;
    }
}
