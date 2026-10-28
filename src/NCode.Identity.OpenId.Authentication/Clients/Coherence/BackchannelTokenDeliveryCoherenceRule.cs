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

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks how a client's effective settings deliver the tokens of a backchannel authentication request: a client whose
/// <c>backchannel_token_delivery_mode</c> is <c>ping</c> or <c>push</c> must register a
/// <c>client_notification_endpoint</c> to be notified at
/// (<see href="https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html">OpenID
/// Connect Client-Initiated Backchannel Authentication Core 1.0</see> §4), and a client that is permitted the CIBA grant
/// must use a mode that <c>backchannel_token_delivery_modes_supported</c> lists when it is set. The mode is
/// <c>poll</c> when none is registered.
/// </summary>
// See ADR-0085 and ADR-0206.
internal sealed class BackchannelTokenDeliveryCoherenceRule : IClientCoherenceRule
{
    private static string[] DeliverySettingNames { get; } =
    [
        OpenIdSettingNames.BackchannelTokenDeliveryMode,
        OpenIdSettingNames.ClientNotificationEndpoint,
        OpenIdSettingNames.BackchannelTokenDeliveryModesSupported,
        OpenIdSettingNames.GrantTypesSupported,
    ];

    /// <summary>
    /// Gets the names of the settings that register the delivery mode and the notification endpoint, of the setting
    /// that lists the supported modes, and of the setting that lists the permitted grant types.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => DeliverySettingNames;

    /// <summary>
    /// Finds the missing notification endpoint of a client that is notified of its tokens, and the delivery mode of a
    /// client permitted the CIBA grant that its effective settings do not support.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations, or an empty list when the token delivery is coherent.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var settings = context.EffectiveSettings;
        List<ClientCoherenceViolation> violations = [];

        var mode =
            settings.TryGetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode, out var registered)
            && !string.IsNullOrEmpty(registered)
                ? registered
                : OpenIdProtocol.BackchannelTokenDeliveryModes.Poll;

        if (
            !string.Equals(
                mode,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
                StringComparison.Ordinal
            )
            && (
                !settings.TryGetValue(
                    OpenIdSettingKeys.ClientNotificationEndpoint,
                    out var endpoint
                ) || string.IsNullOrEmpty(endpoint)
            )
        )
        {
            violations.Add(
                ClientCoherenceViolation.Missing(
                    OpenIdSettingNames.ClientNotificationEndpoint,
                    OpenIdSettingNames.BackchannelTokenDeliveryMode,
                    mode
                )
            );
        }

        var permitsCiba =
            !settings.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out var grantTypes)
            || grantTypes.Contains(OpenIdProtocol.GrantTypes.Ciba, StringComparer.Ordinal);

        if (
            permitsCiba
            && settings.TryGetValue(
                OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
                out var supportedModes
            )
            && !supportedModes.Contains(mode, StringComparer.Ordinal)
        )
        {
            violations.Add(
                new ClientCoherenceViolation(
                    OpenIdSettingNames.BackchannelTokenDeliveryMode,
                    mode,
                    OpenIdSettingNames.BackchannelTokenDeliveryModesSupported
                )
            );
        }

        return violations;
    }
}
