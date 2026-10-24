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
/// Checks that a client's effective <c>dpop_signing_alg_values_supported</c> permits at least one DPoP proof signing
/// algorithm whenever the client requires DPoP-bound access tokens, its <c>require_dpop_bound_access_tokens</c>: a
/// server advertises DPoP support through that list
/// (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-5.1">RFC 9449</see> §5.1), so an empty list means
/// that no DPoP proof can be accepted.
/// </summary>
// See ADR-0097 and ADR-0206.
internal sealed class DPoPBoundAccessTokenCoherenceRule : IClientCoherenceRule
{
    private static string[] BindingSettingNames { get; } =
    [
        OpenIdSettingNames.RequireDPoPBoundAccessTokens,
        OpenIdSettingNames.DPoPSigningAlgValuesSupported,
    ];

    /// <summary>
    /// Gets the names of the setting that requires DPoP-bound access tokens and of the setting that lists the DPoP
    /// proof signing algorithms the server accepts.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => BindingSettingNames;

    /// <summary>
    /// Finds the requirement for DPoP-bound access tokens that a client registers when its effective settings permit
    /// no DPoP proof signing algorithm.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violation, or an empty list when the requirement is supported or not made.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var settings = context.EffectiveSettings;

        if (
            !settings.TryGetValue(OpenIdSettingKeys.RequireDPoPBoundAccessTokens, out var required)
            || !required
        )
        {
            return [];
        }

        if (
            !settings.TryGetValue(
                OpenIdSettingKeys.DPoPSigningAlgValuesSupported,
                out var algorithms
            )
            || algorithms.Count != 0
        )
        {
            return [];
        }

        return
        [
            new ClientCoherenceViolation(
                OpenIdSettingNames.RequireDPoPBoundAccessTokens,
                "true",
                OpenIdSettingNames.DPoPSigningAlgValuesSupported
            ),
        ];
    }
}
