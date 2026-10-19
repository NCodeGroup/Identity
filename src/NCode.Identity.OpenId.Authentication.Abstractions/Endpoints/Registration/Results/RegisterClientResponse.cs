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
}
