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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client's effective <c>redirect_uri_kinds_supported</c> permits every redirect URI it registers, for
/// both its <c>redirect_uris</c> and its <c>post_logout_redirect_uris</c>.
/// </summary>
// See ADR-0203 and ADR-0206.
internal sealed class RedirectUriKindCoherenceRule(IRedirectUriKindCatalog redirectUriKindCatalog)
    : IClientCoherenceRule
{
    private IRedirectUriKindCatalog RedirectUriKindCatalog { get; } = redirectUriKindCatalog;

    private static string[] RedirectUriSettingNames { get; } =
    [
        OpenIdSettingNames.RedirectUris,
        OpenIdSettingNames.PostLogoutRedirectUris,
        OpenIdSettingNames.RedirectUriKindsSupported,
    ];

    /// <summary>
    /// Gets the names of the settings that register redirect URIs and of the setting that permits their kinds.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => RedirectUriSettingNames;

    /// <summary>
    /// Finds every redirect URI a client registers that its effective settings do not permit.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations, or an empty list when every registered redirect URI is permitted.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var clientSettings = context.EffectiveSettings;
        List<ClientCoherenceViolation> violations = [];

        AddIfNotPermitted(violations, clientSettings, OpenIdSettingKeys.RedirectUris);
        AddIfNotPermitted(violations, clientSettings, OpenIdSettingKeys.PostLogoutRedirectUris);

        return violations;
    }

    /// <summary>
    /// Adds a violation for each redirect URI in the specified list that the redirect URI kind catalog does not permit.
    /// </summary>
    private void AddIfNotPermitted(
        List<ClientCoherenceViolation> violations,
        IReadOnlySettingCollection clientSettings,
        SettingKey<IReadOnlyCollection<string>> key
    )
    {
        if (!clientSettings.TryGetValue(key, out var redirectUris))
            return;

        foreach (var redirectUri in redirectUris)
        {
            if (RedirectUriKindCatalog.IsPermitted(clientSettings, redirectUri))
                continue;

            violations.Add(
                new ClientCoherenceViolation(
                    key.SettingName,
                    redirectUri,
                    OpenIdSettingNames.RedirectUriKindsSupported
                )
            );
        }
    }
}
