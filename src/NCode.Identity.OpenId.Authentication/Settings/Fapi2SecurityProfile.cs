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

using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The OpenID Foundation <b>FAPI 2.0 Security Profile</b>, defined in one place: the setting ceilings it injects into
/// the merge lattice and the request-time requirement objects it composes.
/// </summary>
internal sealed class Fapi2SecurityProfile : ISecurityProfile
{
    // FAPI 2.0 restricts signing to PS256 / ES256 / EdDSA; EdDSA is listed forward-compatibly — it is intersected
    // against the server's actually-supported algorithms, so it only takes effect once EdDSA signing is implemented.
    private static readonly string[] SigningAlgorithms =
    [
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        AlgorithmCodes.DigitalSignature.EcdsaSha256,
        AlgorithmCodes.DigitalSignature.EdDsa,
    ];

    // FAPI 2.0 permits only strong (asymmetric / mutual-TLS) client authentication.
    private static readonly string[] TokenEndpointAuthMethods =
    [
        OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
        OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
        OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
    ];

    /// <inheritdoc />
    public string ProfileName => OpenIdConstants.SecurityProfiles.Fapi2;

    /// <inheritdoc />
    public IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; } =
    [
        // A public client is rejected at the authorization endpoint (the token endpoint rejects it via the permitted
        // token_endpoint_auth_methods_supported ceiling).
        new ConfidentialClientRequirement(),
        // A client_assertion may only target the issuer identifier (FAPI 2.0 §5.3.2.1).
        new IssuerOnlyAssertionAudienceRequirement(),
    ];

    /// <inheritdoc />
    public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling)
    {
        // Every access token must be DPoP- or mutual-TLS-bound (Or floor), enforced at the token endpoint and again at
        // issuance together with every targeted resource server's requirement (ADR-0158).
        ceiling.Set(OpenIdSettingKeys.AccessTokenSenderConstraintRequired, true);

        // Signing algorithms → PS256 / ES256 / EdDSA (Intersect ceilings narrow the advertised/accepted set).
        ceiling.Set(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(
            OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported,
            SigningAlgorithms
        );

        // Strong client authentication only, and code-only / PKCE-S256 authorization (Intersect ceilings).
        ceiling.Set(OpenIdSettingKeys.TokenEndpointAuthMethodsSupported, TokenEndpointAuthMethods);
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

        // Tight clock skew for signed request objects / client assertions (Min ceiling): FAPI 2.0 allows at most a
        // 60-second future tolerance when validating token time claims, so a child may only reduce it further.
        ceiling.Set(OpenIdSettingKeys.ClockSkew, TimeSpan.FromSeconds(60));

        // Pin the client-assertion lifetime at the secure default (Min ceiling) so raising the server-wide value for
        // other clients never extends to a FAPI client (ADR-0124).
        ceiling.Set(OpenIdSettingKeys.ClientAssertionMaxLifetime, TimeSpan.FromMinutes(5));

        // A request object carries 'nbf' and 'exp', at most 60 minutes apart (FAPI 2.0 Message Signing §5.3.1,
        // FAPI 1.0 Advanced §5.2.2); pinned so raising the server-wide values never extends to a FAPI client (ADR-0187).
        ceiling.Set(OpenIdSettingKeys.RequestObjectMaxLifetime, TimeSpan.FromMinutes(60));
        ceiling.Set(OpenIdSettingKeys.RequestObjectNotBeforeRequired, true);
    }
}
