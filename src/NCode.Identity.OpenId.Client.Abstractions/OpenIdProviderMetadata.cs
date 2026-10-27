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
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Contains the OpenID provider metadata document published at the discovery endpoint (OpenID Connect Discovery 1.0,
/// RFC 8414), limited to the members the protocol client uses. Any other member is kept in <see cref="ExtensionData"/>.
/// </summary>
[PublicAPI]
public sealed class OpenIdProviderMetadata
{
    /// <summary>
    /// Gets the issuer identifier of the provider (OpenID Connect Discovery 1.0 §3, RFC 8414 §2).
    /// </summary>
    [JsonPropertyName("issuer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the URL of the authorization endpoint.
    /// </summary>
    [JsonPropertyName("authorization_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthorizationEndpoint { get; init; }

    /// <summary>
    /// Gets the URL of the token endpoint.
    /// </summary>
    [JsonPropertyName("token_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenEndpoint { get; init; }

    /// <summary>
    /// Gets the URL of the UserInfo endpoint.
    /// </summary>
    [JsonPropertyName("userinfo_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserInfoEndpoint { get; init; }

    /// <summary>
    /// Gets the URL of the provider's JSON Web Key Set document.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JwksUri { get; init; }

    /// <summary>
    /// Gets the URL of the RP-initiated logout endpoint (OpenID Connect RP-Initiated Logout 1.0).
    /// </summary>
    [JsonPropertyName("end_session_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndSessionEndpoint { get; init; }

    /// <summary>
    /// Gets the URL of the token revocation endpoint (RFC 7009).
    /// </summary>
    [JsonPropertyName("revocation_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RevocationEndpoint { get; init; }

    /// <summary>
    /// Gets the URL of the pushed authorization request endpoint (RFC 9126).
    /// </summary>
    [JsonPropertyName("pushed_authorization_request_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PushedAuthorizationRequestEndpoint { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider accepts authorization requests only when pushed (RFC 9126 §5).
    /// </summary>
    [JsonPropertyName("require_pushed_authorization_requests")]
    public bool RequirePushedAuthorizationRequests { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider returns the <c>iss</c> parameter in authorization responses (RFC 9207).
    /// </summary>
    [JsonPropertyName("authorization_response_iss_parameter_supported")]
    public bool AuthorizationResponseIssParameterSupported { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider supports back-channel logout (OpenID Connect Back-Channel Logout 1.0).
    /// </summary>
    [JsonPropertyName("backchannel_logout_supported")]
    public bool BackchannelLogoutSupported { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider includes <c>sid</c> in logout tokens (OpenID Connect Back-Channel Logout 1.0).
    /// </summary>
    [JsonPropertyName("backchannel_logout_session_supported")]
    public bool BackchannelLogoutSessionSupported { get; init; }

    /// <summary>
    /// Gets the scope values the provider supports.
    /// </summary>
    [JsonPropertyName("scopes_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ScopesSupported { get; init; }

    /// <summary>
    /// Gets the <c>response_type</c> values the provider supports.
    /// </summary>
    [JsonPropertyName("response_types_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ResponseTypesSupported { get; init; }

    /// <summary>
    /// Gets the grant types the provider supports.
    /// </summary>
    [JsonPropertyName("grant_types_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? GrantTypesSupported { get; init; }

    /// <summary>
    /// Gets the PKCE code challenge methods the provider supports (RFC 7636).
    /// </summary>
    [JsonPropertyName("code_challenge_methods_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CodeChallengeMethodsSupported { get; init; }

    /// <summary>
    /// Gets the client authentication methods the token endpoint supports.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? TokenEndpointAuthMethodsSupported { get; init; }

    /// <summary>
    /// Gets the JWS algorithms the token endpoint supports for <c>private_key_jwt</c> client assertions.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_signing_alg_values_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? TokenEndpointAuthSigningAlgValuesSupported { get; init; }

    /// <summary>
    /// Gets the JWS algorithms the provider uses to sign ID tokens.
    /// </summary>
    [JsonPropertyName("id_token_signing_alg_values_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IdTokenSigningAlgValuesSupported { get; init; }

    /// <summary>
    /// Gets the JWS algorithms the provider supports for DPoP proofs (RFC 9449).
    /// </summary>
    [JsonPropertyName("dpop_signing_alg_values_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DpopSigningAlgValuesSupported { get; init; }

    /// <summary>
    /// Gets or sets the metadata members that have no dedicated property.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
