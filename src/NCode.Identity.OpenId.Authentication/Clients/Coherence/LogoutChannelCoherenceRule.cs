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
/// Checks that the logout channels a client registers are ones its effective settings support: a
/// <c>backchannel_logout_uri</c> needs <c>backchannel_logout_supported</c>, and a <c>frontchannel_logout_uri</c> needs
/// <c>frontchannel_logout_supported</c> (OpenID Connect Back-Channel Logout 1.0 §2.1, Front-Channel Logout 1.0 §3); a
/// client that requires the session identifier, through <c>backchannel_logout_session_required</c> or
/// <c>frontchannel_logout_session_required</c>, needs the matching <c>*_session_supported</c>. Without this rule the
/// server would advertise one thing in discovery and deliver another, or never deliver what the client waits for.
/// </summary>
// See ADR-0206 and ADR-0088.
internal sealed class LogoutChannelCoherenceRule : IClientCoherenceRule
{
    private static string[] LogoutSettingNames { get; } =
    [
        OpenIdSettingNames.BackChannelLogoutUri,
        OpenIdSettingNames.BackChannelLogoutSupported,
        OpenIdSettingNames.BackChannelLogoutSessionRequired,
        OpenIdSettingNames.BackChannelLogoutSessionSupported,
        OpenIdSettingNames.FrontChannelLogoutUri,
        OpenIdSettingNames.FrontChannelLogoutSupported,
        OpenIdSettingNames.FrontChannelLogoutSessionRequired,
        OpenIdSettingNames.FrontChannelLogoutSessionSupported,
    ];

    /// <summary>
    /// Gets the names of the client's logout registrations and of the settings that say which logout channels, and
    /// which session identifiers, the server supports.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => LogoutSettingNames;

    /// <summary>
    /// Finds each logout registration of the client that its effective settings do not support.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations, or an empty list when every registration is supported.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var registered = context.RegisteredSettings;
        var effective = context.EffectiveSettings;
        var violations = new List<ClientCoherenceViolation>();

        AddUriViolation(
            registered,
            effective,
            OpenIdSettingKeys.BackChannelLogoutUri,
            OpenIdSettingKeys.BackChannelLogoutSupported,
            violations
        );
        AddSessionViolation(
            registered,
            effective,
            OpenIdSettingKeys.BackChannelLogoutSessionRequired,
            OpenIdSettingKeys.BackChannelLogoutSessionSupported,
            violations
        );
        AddUriViolation(
            registered,
            effective,
            OpenIdSettingKeys.FrontChannelLogoutUri,
            OpenIdSettingKeys.FrontChannelLogoutSupported,
            violations
        );
        AddSessionViolation(
            registered,
            effective,
            OpenIdSettingKeys.FrontChannelLogoutSessionRequired,
            OpenIdSettingKeys.FrontChannelLogoutSessionSupported,
            violations
        );

        return violations;
    }

    private static void AddUriViolation(
        IReadOnlySettingCollection registered,
        IReadOnlySettingCollection effective,
        SettingKey<string> uriKey,
        SettingKey<bool> supportedKey,
        List<ClientCoherenceViolation> violations
    )
    {
        if (!registered.TryGetValue(uriKey, out var uri) || string.IsNullOrEmpty(uri))
            return;

        if (!effective.TryGetValue(supportedKey, out var supported) || supported)
            return;

        violations.Add(
            new ClientCoherenceViolation(uriKey.SettingName, uri, supportedKey.SettingName)
        );
    }

    private static void AddSessionViolation(
        IReadOnlySettingCollection registered,
        IReadOnlySettingCollection effective,
        SettingKey<bool> requiredKey,
        SettingKey<bool> supportedKey,
        List<ClientCoherenceViolation> violations
    )
    {
        if (!registered.TryGetValue(requiredKey, out var required) || !required)
            return;

        if (!effective.TryGetValue(supportedKey, out var supported) || supported)
            return;

        violations.Add(
            new ClientCoherenceViolation(requiredKey.SettingName, "true", supportedKey.SettingName)
        );
    }
}
