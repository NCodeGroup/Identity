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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The production guardrail profile, defined in one place: it removes the development conveniences that a production
/// tenant must not keep, namely plain <c>http</c> redirect URIs and anonymous dynamic client registration, and turns
/// on deletion protection by default.
/// </summary>
// See ADR-0289.
internal sealed class ProductionSecurityProfile : ISecurityProfile
{
    /// <inheritdoc />
    public string ProfileName => OpenIdConstants.SecurityProfiles.Production;

    /// <inheritdoc />
    public IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; } = [];

    /// <inheritdoc />
    public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling)
    {
        // No plain http redirect URI except a native client's loopback redirect (RFC 8252 §7.3); private-use schemes
        // stay permitted.
        ceiling.Set(
            OpenIdSettingKeys.RedirectUriKindsSupported,
            (IReadOnlyCollection<string>)
                [
                    OpenIdConstants.RedirectUriKinds.Https,
                    OpenIdConstants.RedirectUriKinds.Loopback,
                    OpenIdConstants.RedirectUriKinds.PrivateUse,
                ]
        );

        // A production tenant registers its clients through an authenticated caller only.
        ceiling.Set(OpenIdSettingKeys.RegistrationEndpointAllowAnonymous, false);
    }

    /// <inheritdoc />
    public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults)
    {
        // A production tenant cannot be deleted until its owner turns this off.
        defaults.Set(OpenIdSettingKeys.DeletionProtection, true);
    }
}
