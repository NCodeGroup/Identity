// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Reads the authentication scheme that holds the end-user's OP login, so every endpoint that reads or clears the login
/// — the authorize endpoint and RP-Initiated Logout — uses the same one.
/// </summary>
internal static class LoginSchemeExtensions
{
    extension(IReadOnlySettingCollection settings)
    {
        /// <summary>
        /// Gets the <c>authorization_authenticate_scheme</c> setting: the scheme the end-user's OP login is
        /// authenticated with, or <see langword="null"/> for the host's default scheme when the setting is empty.
        /// </summary>
        /// <returns>The scheme's name, or <see langword="null"/> for the host's default scheme.</returns>
        public string? GetLoginScheme()
        {
            ArgumentNullException.ThrowIfNull(settings);

            var scheme = settings.GetValue(OpenIdSettingKeys.AuthorizationAuthenticateScheme);
            return string.IsNullOrEmpty(scheme) ? null : scheme;
        }
    }
}
