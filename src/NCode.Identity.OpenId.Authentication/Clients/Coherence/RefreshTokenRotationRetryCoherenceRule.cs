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

using System.Globalization;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client whose effective <c>refresh_token_rotation_retry_required</c> is <see langword="true"/> and
/// whose refresh tokens can rotate has a positive <c>refresh_token_reuse_interval</c>, so that it can retry a rotation.
/// The refresh tokens of a client can rotate under the <c>always</c> rotation policy, and a public client's under the
/// <c>unbound</c> rotation policy.
/// </summary>
// See ADR-0206 and ADR-0217.
internal sealed class RefreshTokenRotationRetryCoherenceRule : IClientCoherenceRule
{
    private static string[] RetrySettingNames { get; } =
    [
        OpenIdSettingNames.RefreshTokenRotationRetryRequired,
        OpenIdSettingNames.RefreshTokenRotationPolicy,
        OpenIdSettingNames.RefreshTokenReuseInterval,
        OpenIdSettingNames.TokenEndpointAuthMethod,
    ];

    /// <summary>
    /// Gets the names of the setting that requires a retry, of the setting that selects the rotation policy, of the
    /// setting that sets the reuse interval, and of the setting that registers the client-authentication method.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => RetrySettingNames;

    /// <summary>
    /// Finds the reuse interval that a client whose refresh tokens can rotate must have when it requires a retry but
    /// its effective settings turn the interval off.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violation, or an empty list when a retry is not required, cannot be needed, or is possible.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var settings = context.EffectiveSettings;

        if (
            !settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationRetryRequired)
            || !RefreshTokenRotationRequirement.CanRotate(settings)
        )
        {
            return [];
        }

        var interval = settings.GetValue(OpenIdSettingKeys.RefreshTokenReuseInterval);
        if (interval > TimeSpan.Zero)
            return [];

        var policy = settings.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy);
        return
        [
            new ClientCoherenceViolation(
                OpenIdSettingNames.RefreshTokenReuseInterval,
                interval.ToString("c", CultureInfo.InvariantCulture),
                OpenIdSettingNames.RefreshTokenRotationRetryRequired
            )
            {
                Description =
                    $"The effective '{OpenIdSettingNames.RefreshTokenRotationRetryRequired}' value 'true' requires a positive '{OpenIdSettingNames.RefreshTokenReuseInterval}', because the client's refresh tokens rotate under the effective '{OpenIdSettingNames.RefreshTokenRotationPolicy}' value '{policy}'.",
            },
        ];
    }
}
