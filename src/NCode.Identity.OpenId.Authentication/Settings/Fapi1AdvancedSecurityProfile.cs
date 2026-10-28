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
/// The OpenID Foundation <b>FAPI 1.0 Advanced</b> Security Profile (Final, Part 2 §5.2.2), defined in one place: the
/// setting ceilings it injects into the merge lattice, the defaults it supplies, and the request-time requirement
/// objects it composes.
/// </summary>
internal sealed class Fapi1AdvancedSecurityProfile : ISecurityProfile
{
    // FAPI 1.0 Advanced §8.6 restricts signing to PS256 / ES256 (no EdDSA, unlike FAPI 2.0).
    private static readonly string[] SigningAlgorithms =
    [
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        AlgorithmCodes.DigitalSignature.EcdsaSha256,
    ];

    // FAPI 1.0 Advanced §5.2.2-14 permits only mutual-TLS or private_key_jwt client authentication.
    private static readonly string[] TokenEndpointAuthMethods =
    [
        OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
        OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth,
        OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
    ];

    // FAPI 1.0 Advanced §5.2.2-1/2: 'code id_token', or 'code' with a JWT-secured response mode (the requirement below).
    private static readonly string[] ResponseTypes =
    [
        OpenIdProtocol.ResponseTypes.Code,
        OpenIdProtocol.ResponseTypes.CodeIdToken,
    ];

    /// <inheritdoc />
    public string ProfileName => OpenIdConstants.SecurityProfiles.Fapi1Advanced;

    /// <inheritdoc />
    public IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; } =
    [
        // A public client is rejected at the authorization endpoint (the token endpoint rejects it via the permitted
        // token_endpoint_auth_methods_supported ceiling) (§5.2.2-14).
        new ConfidentialClientRequirement(),
        // A plain 'code' response must be JWT-secured (JARM); 'code id_token' is protected by its ID token (§5.2.2-2).
        new JwtSecuredAuthorizationResponseRequirement(),
        // 'nonce' with the 'openid' scope, 'state' without it (§5.2.2.2, §5.2.2.3).
        new NonceOrStateRequirement(),
        // A request object is addressed to the issuer, which no client setting may replace (§5.2.2).
        new IssuerOnlyRequestObjectAudienceRequirement(),
    ];

    /// <inheritdoc />
    public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling)
    {
        // Every access token must be sender-constrained (§5.2.2-5), and FAPI 1.0's mechanism is mutual TLS (§5.2.2-6,
        // RFC 8705): requiring every client's token to be certificate-bound makes a DPoP-only token request fail (Or
        // floors, ADR-0158).
        ceiling.Set(OpenIdSettingKeys.AccessTokenSenderConstraintRequired, true);
        ceiling.Set(OpenIdSettingKeys.RequireTlsClientCertificateBoundAccessTokens, true);

        // Signing algorithms → PS256 / ES256 (Intersect ceilings narrow the advertised/accepted set).
        ceiling.Set(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.AuthorizationSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported, SigningAlgorithms);
        ceiling.Set(
            OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported,
            SigningAlgorithms
        );

        // Strong client authentication, the two permitted response types, and PKCE-S256 (Intersect ceilings).
        ceiling.Set(OpenIdSettingKeys.TokenEndpointAuthMethodsSupported, TokenEndpointAuthMethods);
        ceiling.Set(OpenIdSettingKeys.ResponseTypesSupported, ResponseTypes);
        ceiling.Set(
            OpenIdSettingKeys.CodeChallengeMethodsSupported,
            (IReadOnlyCollection<string>)[OpenIdProtocol.CodeChallengeMethods.S256]
        );

        // Require PKCE, PAR and a signed request object (Or floors cannot be un-required by a child) (§5.2.2-1/18/19).
        ceiling.Set(OpenIdSettingKeys.RequireCodeChallenge, true);
        ceiling.Set(OpenIdSettingKeys.RequirePushedAuthorizationRequests, true);
        ceiling.Set(OpenIdSettingKeys.RequireRequestObject, true);

        // Short-lived authorization code and PAR request_uri (Min ceilings can only be shortened by a child).
        ceiling.Set(OpenIdSettingKeys.AuthorizationCodeLifetime, TimeSpan.FromSeconds(60));
        ceiling.Set(OpenIdSettingKeys.PushedAuthorizationRequestLifetime, TimeSpan.FromSeconds(90));

        // Tight clock skew for signed request objects / client assertions (Min ceiling).
        ceiling.Set(OpenIdSettingKeys.ClockSkew, TimeSpan.FromSeconds(60));

        // Pin the client-assertion lifetime at the secure default (Min ceiling) (ADR-0124).
        ceiling.Set(OpenIdSettingKeys.ClientAssertionMaxLifetime, TimeSpan.FromMinutes(5));

        // A request object carries 'nbf' and 'exp', at most 60 minutes apart (§5.2.2-13, -17) (ADR-0187).
        ceiling.Set(OpenIdSettingKeys.RequestObjectMaxLifetime, TimeSpan.FromMinutes(60));
        ceiling.Set(OpenIdSettingKeys.RequestObjectNotBeforeRequired, true);

        // FAPI 1.0 Part 1 §5.2.2-20 and §7.5 permit only https redirect URIs: no http, loopback, or private-use
        // redirection, so a loopback URI's port never varies either (ADR-0203).
        ceiling.Set(OpenIdSettingKeys.AllowLoopbackRedirect, false);
        ceiling.Set(
            OpenIdSettingKeys.RedirectUriKindsSupported,
            (IReadOnlyCollection<string>)[OpenIdConstants.RedirectUriKinds.Https]
        );
    }

    /// <inheritdoc />
    public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults)
    {
        // The protocol default for the signed responses is RS256, which the signing ceilings above exclude, so a client
        // that registers no algorithm would fail at issuance; PS256 is the compliant default (ADR-0148, ADR-0155).
        defaults.Set(
            OpenIdSettingKeys.IdTokenSignedResponseAlg,
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256
        );
        defaults.Set(
            OpenIdSettingKeys.IntrospectionSignedResponseAlg,
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256
        );
        defaults.Set(
            OpenIdSettingKeys.AuthorizationSignedResponseAlg,
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256
        );

        // FAPI 1.0 Advanced §5.2.2-6 requires support for mutual-TLS certificate-bound access tokens, so the server
        // supports them by default; this is a default, so an operator may turn it off.
        defaults.Set(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens, true);
    }
}
