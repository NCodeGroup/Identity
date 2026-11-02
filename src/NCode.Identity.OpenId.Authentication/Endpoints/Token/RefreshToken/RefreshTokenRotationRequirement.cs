// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;

/// <summary>
/// Decides whether a refresh token must be rotated, that is replaced by a new refresh token when it is used. Under
/// the <c>always</c> policy every refresh token is rotated. Under the <c>unbound</c> policy a refresh token is rotated
/// unless it is bound to a proof-of-possession key or to client authentication: a public client's refresh token
/// without a DPoP or certificate binding is always rotated, as RFC 9700 requires, and no setting turns that off.
/// </summary>
// See ADR-1216.
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

    /// <summary>
    /// Gets a value indicating whether a client with these effective settings is issued refresh tokens that can rotate:
    /// every one under <c>always</c>, and a public client's under <c>unbound</c>.
    /// </summary>
    /// <param name="settings">The effective settings of the client.</param>
    /// <returns><see langword="true"/> when a refresh token issued to the client can be rotated.</returns>
    public static bool CanRotate(IReadOnlySettingCollection settings)
    {
        var policy = settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy);
        if (
            string.Equals(
                policy,
                OpenIdConstants.RefreshTokenRotationPolicy.Always,
                StringComparison.Ordinal
            )
        )
            return true;

        return string.Equals(
                policy,
                OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
                StringComparison.Ordinal
            )
            && string.Equals(
                settings.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod),
                OpenIdProtocol.ClientAuthenticationMethods.None,
                StringComparison.Ordinal
            );
    }
}
