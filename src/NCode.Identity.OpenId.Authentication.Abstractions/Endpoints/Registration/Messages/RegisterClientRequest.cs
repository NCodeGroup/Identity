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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration.Messages;

/// <summary>
/// Contains the client metadata submitted to the dynamic client registration endpoint (RFC 7591 §2), used both for
/// the initial registration request and for a full replacement of an existing registration (RFC 7592).
/// </summary>
[PublicAPI]
public sealed class RegisterClientRequest
{
    /// <summary>
    /// Gets or sets the array of redirection URI strings the client may use in authorization requests.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RedirectUris { get; init; }

    /// <summary>
    /// Gets or sets the requested client authentication method for the token endpoint (for example
    /// <c>client_secret_basic</c>, <c>client_secret_post</c>, or <c>none</c> for a public client).
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenEndpointAuthMethod { get; init; }

    /// <summary>
    /// Gets or sets the array of OAuth 2.0 grant types the client may use.
    /// </summary>
    [JsonPropertyName("grant_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GrantTypes { get; init; }

    /// <summary>
    /// Gets or sets the array of OAuth 2.0 response types the client may use.
    /// </summary>
    [JsonPropertyName("response_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ResponseTypes { get; init; }

    /// <summary>
    /// Gets or sets the human-readable name of the client to be presented to the end user.
    /// </summary>
    [JsonPropertyName("client_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientName { get; init; }

    /// <summary>
    /// Gets or sets the URL of the home page of the client.
    /// </summary>
    [JsonPropertyName("client_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientUri { get; init; }

    /// <summary>
    /// Gets or sets the URL that references a logo for the client.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogoUri { get; init; }

    /// <summary>
    /// Gets or sets the space-separated list of scope values the client may use when requesting access tokens.
    /// </summary>
    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; init; }

    /// <summary>
    /// Gets or sets the array of ways to contact people responsible for the client, typically email addresses.
    /// </summary>
    [JsonPropertyName("contacts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Contacts { get; init; }

    /// <summary>
    /// Gets or sets the URL that points to a human-readable terms of service document for the client.
    /// </summary>
    [JsonPropertyName("tos_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TosUri { get; init; }

    /// <summary>
    /// Gets or sets the URL that points to a human-readable privacy policy document for the client.
    /// </summary>
    [JsonPropertyName("policy_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyUri { get; init; }

    /// <summary>
    /// Gets or sets the URL referencing the client's JSON Web Key Set document, which contains the client's public keys.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JwksUri { get; init; }

    /// <summary>
    /// Gets or sets the client's JSON Web Key Set document value, which contains the client's public keys.
    /// </summary>
    [JsonPropertyName("jwks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Jwks { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier string assigned by the client developer or software publisher, used to
    /// identify the client software to be registered.
    /// </summary>
    [JsonPropertyName("software_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareId { get; init; }

    /// <summary>
    /// Gets or sets the version identifier string for the client software identified by <see cref="SoftwareId"/>.
    /// </summary>
    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized metadata members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
