// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client's effective <c>token_endpoint_auth_methods_supported</c> permits the client-authentication
/// method the client is registered to use, its <c>token_endpoint_auth_method</c>. An unset
/// <c>token_endpoint_auth_methods_supported</c> permits every method.
/// </summary>
// See ADR-1145 and ADR-1206.
internal sealed class TokenEndpointAuthMethodCoherenceRule : IClientCoherenceRule
{
    private static string[] AuthMethodSettingNames { get; } =
    [
        OpenIdSettingNames.TokenEndpointAuthMethod,
        OpenIdSettingNames.TokenEndpointAuthMethodsSupported,
    ];

    /// <summary>
    /// Gets the names of the setting that registers the client-authentication method and of the setting that permits
    /// the methods.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => AuthMethodSettingNames;

    /// <summary>
    /// Finds the client-authentication method a client registers when its effective settings do not permit it.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violation, or an empty list when the registered method is permitted.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var clientSettings = context.EffectiveSettings;
        var method = clientSettings.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod);

        if (
            !clientSettings.TryGetValue(
                OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                out var permittedMethods
            ) || permittedMethods.Contains(method, StringComparer.Ordinal)
        )
        {
            return [];
        }

        return
        [
            new ClientCoherenceViolation(
                OpenIdSettingNames.TokenEndpointAuthMethod,
                method,
                OpenIdSettingNames.TokenEndpointAuthMethodsSupported
            ),
        ];
    }
}
