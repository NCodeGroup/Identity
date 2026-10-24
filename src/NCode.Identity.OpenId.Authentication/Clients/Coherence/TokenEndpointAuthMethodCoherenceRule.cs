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
/// Checks that a client's effective <c>token_endpoint_auth_methods_supported</c> permits the client-authentication
/// method the client is registered to use, its <c>token_endpoint_auth_method</c>. An unset
/// <c>token_endpoint_auth_methods_supported</c> permits every method.
/// </summary>
// See ADR-0145 and ADR-0206.
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
