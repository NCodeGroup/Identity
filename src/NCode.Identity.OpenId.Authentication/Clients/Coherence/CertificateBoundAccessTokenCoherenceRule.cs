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
/// Checks that a client's effective <c>tls_client_certificate_bound_access_tokens</c> states that the server supports
/// certificate-bound access tokens whenever the client requires them, its
/// <c>require_tls_client_certificate_bound_access_tokens</c>.
/// </summary>
// See ADR-0097 and ADR-0206.
internal sealed class CertificateBoundAccessTokenCoherenceRule : IClientCoherenceRule
{
    private static string[] BindingSettingNames { get; } =
    [
        OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens,
        OpenIdSettingNames.TlsClientCertificateBoundAccessTokens,
    ];

    /// <summary>
    /// Gets the names of the setting that requires certificate-bound access tokens and of the setting that states
    /// whether the server supports them.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => BindingSettingNames;

    /// <summary>
    /// Finds the requirement for certificate-bound access tokens that a client registers when its effective settings
    /// state that the server does not support them.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violation, or an empty list when the requirement is supported or not made.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var clientSettings = context.EffectiveSettings;

        if (
            !clientSettings.GetValue(OpenIdSettingKeys.RequireTlsClientCertificateBoundAccessTokens)
            || clientSettings.GetValue(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens)
        )
        {
            return [];
        }

        return
        [
            new ClientCoherenceViolation(
                OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens,
                "true",
                OpenIdSettingNames.TlsClientCertificateBoundAccessTokens
            ),
        ];
    }
}
