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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration.Results;

/// <summary>
/// Contains the client information response returned by the dynamic client registration endpoint (RFC 7591 §3.2.1)
/// and by the per-client registration management endpoint (RFC 7592). It echoes the registered client metadata and
/// adds the server-assigned registration fields.
/// </summary>
[PublicAPI]
public sealed record RegisterClientResponse
{
    /// <summary>
    /// Gets the unique client identifier assigned by the authorization server.
    /// </summary>
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the client secret assigned by the authorization server, when the client is confidential. The
    /// secret is revealed only in the registration response and is never returned again.
    /// </summary>
    [JsonPropertyName("client_secret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets the time at which the client identifier was issued, as the number of seconds since the Unix epoch.
    /// </summary>
    [JsonPropertyName("client_id_issued_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ClientIdIssuedAt { get; init; }

    /// <summary>
    /// Gets the time at which the client secret expires, as the number of seconds since the Unix epoch, or
    /// <c>0</c> if the secret does not expire. Present only when a client secret was issued.
    /// </summary>
    [JsonPropertyName("client_secret_expires_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ClientSecretExpiresAt { get; init; }

    /// <summary>
    /// Gets the access token the client uses to read, update, or delete its own registration at the
    /// registration management endpoint (RFC 7592). Revealed only in the registration response.
    /// </summary>
    [JsonPropertyName("registration_access_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistrationAccessToken { get; init; }

    /// <summary>
    /// Gets the fully qualified URL of the client's registration management endpoint (RFC 7592).
    /// </summary>
    [JsonPropertyName("registration_client_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistrationClientUri { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client is bound to and introspects for, or <c>null</c> for an
    /// ordinary client. The binding comes from the initial access token the client registered with and never changes
    /// (ADR-0192), so the server reports it and ignores the member in a request.
    /// </summary>
    [JsonPropertyName("resource_server_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceServerId { get; init; }

    /// <summary>
    /// Gets the array of redirection URI strings the client may use in authorization requests.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RedirectUris { get; init; }

    /// <summary>
    /// Gets the client authentication method registered for the token endpoint.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenEndpointAuthMethod { get; init; }

    /// <summary>
    /// Gets the array of OAuth 2.0 grant types registered for the client.
    /// </summary>
    [JsonPropertyName("grant_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GrantTypes { get; init; }

    /// <summary>
    /// Gets the array of OAuth 2.0 response types registered for the client, each a whole <c>response_type</c> value
    /// in canonical form (its names in ordinal order); empty for a client that does not use the authorization
    /// endpoint.
    /// </summary>
    [JsonPropertyName("response_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ResponseTypes { get; init; }

    /// <summary>
    /// Gets the subject identifier type (<c>public</c> or <c>pairwise</c>) registered for the client
    /// (OpenID Connect Core 1.0 §8).
    /// </summary>
    [JsonPropertyName("subject_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubjectType { get; init; }

    /// <summary>
    /// Gets the URL referencing the client's sector identifier document registered for the client
    /// (OpenID Connect Core 1.0 §8.1).
    /// </summary>
    [JsonPropertyName("sector_identifier_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SectorIdentifierUri { get; init; }

    /// <summary>
    /// Gets the human-readable name of the client to be presented to the end user.
    /// </summary>
    [JsonPropertyName("client_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientName { get; init; }

    /// <summary>
    /// Gets the URL of the home page of the client.
    /// </summary>
    [JsonPropertyName("client_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientUri { get; init; }

    /// <summary>
    /// Gets the URL that references a logo for the client.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogoUri { get; init; }

    /// <summary>
    /// Gets the space-separated list of scope values registered for the client.
    /// </summary>
    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the array of ways to contact people responsible for the client, typically email addresses.
    /// </summary>
    [JsonPropertyName("contacts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Contacts { get; init; }

    /// <summary>
    /// Gets the URL that points to a human-readable terms of service document for the client.
    /// </summary>
    [JsonPropertyName("tos_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TosUri { get; init; }

    /// <summary>
    /// Gets the URL that points to a human-readable privacy policy document for the client.
    /// </summary>
    [JsonPropertyName("policy_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyUri { get; init; }

    /// <summary>
    /// Gets the URL referencing the client's JSON Web Key Set document, which contains the client's public keys.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JwksUri { get; init; }

    /// <summary>
    /// Gets the client's JSON Web Key Set document value, which contains the client's public keys.
    /// </summary>
    [JsonPropertyName("jwks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Jwks { get; init; }

    /// <summary>
    /// Gets the unique identifier string assigned by the client developer or software publisher.
    /// </summary>
    [JsonPropertyName("software_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareId { get; init; }

    /// <summary>
    /// Gets the version identifier string for the client software identified by <see cref="SoftwareId"/>.
    /// </summary>
    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> the client requires for signing its ID tokens (OpenID Connect Dynamic Client Registration 1.0 §2); <c>RS256</c> when omitted.
    /// </summary>
    [JsonPropertyName("id_token_signed_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdTokenSignedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>alg</c> the client requires for encrypting its ID tokens to its public key (OpenID Connect Dynamic Client Registration 1.0 §2); ID tokens are not encrypted when omitted.
    /// </summary>
    [JsonPropertyName("id_token_encrypted_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdTokenEncryptedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>enc</c> the client requires for encrypting its ID tokens (OpenID Connect Dynamic Client Registration 1.0 §2); <c>A128CBC-HS256</c> when omitted, and allowed only with <see cref="IdTokenEncryptedResponseAlg"/>.
    /// </summary>
    [JsonPropertyName("id_token_encrypted_response_enc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdTokenEncryptedResponseEnc { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> the client requires for signing its UserInfo responses (OpenID Connect Dynamic Client Registration 1.0 §2); UserInfo responses are not signed when omitted.
    /// </summary>
    [JsonPropertyName("userinfo_signed_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserInfoSignedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>alg</c> the client requires for encrypting its UserInfo responses to its public key (OpenID Connect Dynamic Client Registration 1.0 §2); UserInfo responses are not encrypted when omitted.
    /// </summary>
    [JsonPropertyName("userinfo_encrypted_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserInfoEncryptedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>enc</c> the client requires for encrypting its UserInfo responses (OpenID Connect Dynamic Client Registration 1.0 §2); <c>A128CBC-HS256</c> when omitted, and allowed only with <see cref="UserInfoEncryptedResponseAlg"/>.
    /// </summary>
    [JsonPropertyName("userinfo_encrypted_response_enc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserInfoEncryptedResponseEnc { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> the client requires for signing its JWT introspection responses (RFC 9701 §6); <c>RS256</c> when omitted. Registered only by a client bound to a resource server (ADR-0192).
    /// </summary>
    [JsonPropertyName("introspection_signed_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IntrospectionSignedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>alg</c> the client requires for encrypting its JWT introspection responses to its public key (RFC 9701 §6); introspection responses are not encrypted when omitted. Registered only by a client bound to a resource server (ADR-0192).
    /// </summary>
    [JsonPropertyName("introspection_encrypted_response_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IntrospectionEncryptedResponseAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>enc</c> the client requires for encrypting its JWT introspection responses (RFC 9701 §6); <c>A128CBC-HS256</c> when omitted, and allowed only with <see cref="IntrospectionEncryptedResponseAlg"/>.
    /// </summary>
    [JsonPropertyName("introspection_encrypted_response_enc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IntrospectionEncryptedResponseEnc { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> every request object from the client must be signed with (OpenID Connect Dynamic Client Registration 1.0 §2); any algorithm the server permits when omitted.
    /// </summary>
    [JsonPropertyName("request_object_signing_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestObjectSigningAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>alg</c> the client uses when it encrypts a request object to the server (OpenID Connect Dynamic Client Registration 1.0 §2); an encrypted request object must use it when registered.
    /// </summary>
    [JsonPropertyName("request_object_encryption_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestObjectEncryptionAlg { get; init; }

    /// <summary>
    /// Gets the JWE <c>enc</c> the client uses when it encrypts a request object to the server (OpenID Connect Dynamic Client Registration 1.0 §2); an encrypted request object must use it when registered.
    /// </summary>
    [JsonPropertyName("request_object_encryption_enc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestObjectEncryptionEnc { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> the client signs its token endpoint authentication assertions with (OpenID Connect Dynamic Client Registration 1.0 §2); any algorithm the server permits when omitted.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_signing_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenEndpointAuthSigningAlg { get; init; }

    /// <summary>
    /// Gets the CIBA token delivery mode the client uses: <c>poll</c>, <c>ping</c>, or <c>push</c> (CIBA Core 1.0 §4); <c>poll</c> when omitted.
    /// </summary>
    [JsonPropertyName("backchannel_token_delivery_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BackchannelTokenDeliveryMode { get; init; }

    /// <summary>
    /// Gets the HTTPS endpoint the server sends CIBA ping and push notifications to (CIBA Core 1.0 §4); required for the <c>ping</c> and <c>push</c> delivery modes.
    /// </summary>
    [JsonPropertyName("backchannel_client_notification_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BackchannelClientNotificationEndpoint { get; init; }

    /// <summary>
    /// Gets the asymmetric JWS <c>alg</c> every signed CIBA authentication request from the client must use (CIBA Core 1.0 §4); any algorithm the server permits when omitted.
    /// </summary>
    [JsonPropertyName("backchannel_authentication_request_signing_alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BackchannelAuthenticationRequestSigningAlg { get; init; }

    /// <summary>
    /// Gets whether the client supports the CIBA <c>user_code</c> parameter (CIBA Core 1.0 §4); <see langword="false"/> when omitted, and <see langword="true"/> only when the server supports it.
    /// </summary>
    [JsonPropertyName("backchannel_user_code_parameter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BackchannelUserCodeParameter { get; init; }
}
