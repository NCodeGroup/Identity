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

using System.Text.Json;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="ISecurityProfileSettingsExpander"/>. The only recognized profile
/// is <c>fapi2</c>; when a scope declares it, the FAPI 2.0 ceiling settings are merged into that scope's settings so the
/// existing floor/ceiling merge lattice enforces them down the cascade.
/// </summary>
internal sealed class DefaultSecurityProfileSettingsExpander(
    ISettingSerializer settingSerializer,
    ISettingCollectionFactory settingCollectionFactory
) : ISecurityProfileSettingsExpander
{
    // FAPI 2.0 restricts signing to PS256 / ES256 / EdDSA; EdDSA is listed forward-compatibly — it is intersected
    // against the server's actually-supported algorithms, so it only takes effect once EdDSA signing is implemented.
    private static readonly string[] Fapi2SigningAlgorithms =
    [
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        AlgorithmCodes.DigitalSignature.EcdsaSha256,
        AlgorithmCodes.DigitalSignature.EdDsa,
    ];

    // FAPI 2.0 permits only strong (asymmetric / mutual-TLS) client authentication.
    private static readonly string[] Fapi2TokenEndpointAuthMethods =
    [
        OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
        OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
        OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
    ];

    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISettingCollectionFactory SettingCollectionFactory { get; } = settingCollectionFactory;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    ) => Expand(SettingSerializer.DeserializeSettings(settingsJson, jsonOptions));

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> Expand(IReadOnlyCollection<Setting> scopeSettings)
    {
        var collection = SettingCollectionFactory.Create(scopeSettings);

        if (
            !collection.TryGetValue(OpenIdSettingKeys.SecurityProfile, out var profile)
            || !string.Equals(
                profile,
                OpenIdConstants.SecurityProfiles.Fapi2,
                StringComparison.Ordinal
            )
        )
        {
            return scopeSettings;
        }

        return collection.Merge(BuildFapi2Ceiling());
    }

    private ISettingCollection BuildFapi2Ceiling()
    {
        var ceiling = SettingCollectionFactory.Create();

        // Signing algorithms → PS256 / ES256 / EdDSA (Intersect ceilings narrow the advertised/accepted set).
        ceiling.Set(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, Fapi2SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported, Fapi2SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, Fapi2SigningAlgorithms);
        ceiling.Set(
            OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported,
            Fapi2SigningAlgorithms
        );
        ceiling.Set(
            OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported,
            Fapi2SigningAlgorithms
        );

        // Strong client authentication only, and code-only / PKCE-S256 authorization (Intersect ceilings).
        ceiling.Set(
            OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
            Fapi2TokenEndpointAuthMethods
        );
        ceiling.Set(
            OpenIdSettingKeys.ResponseTypesSupported,
            (IReadOnlyCollection<string>)[OpenIdConstants.ResponseTypes.Code]
        );
        ceiling.Set(
            OpenIdSettingKeys.CodeChallengeMethodsSupported,
            (IReadOnlyCollection<string>)[OpenIdConstants.CodeChallengeMethods.S256]
        );

        // Require PKCE and PAR (Or floors cannot be un-required by a child).
        ceiling.Set(OpenIdSettingKeys.RequireCodeChallenge, true);
        ceiling.Set(OpenIdSettingKeys.RequirePushedAuthorizationRequests, true);

        // Short-lived authorization code and PAR request_uri (Min ceilings can only be shortened by a child).
        ceiling.Set(OpenIdSettingKeys.AuthorizationCodeLifetime, TimeSpan.FromSeconds(60));
        ceiling.Set(OpenIdSettingKeys.PushedAuthorizationRequestLifetime, TimeSpan.FromSeconds(90));

        return ceiling;
    }
}
