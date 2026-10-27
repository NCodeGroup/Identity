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
/// Contains a successful token endpoint response (RFC 6749 §5.1). Any other member is kept in <see cref="ExtensionData"/>.
/// </summary>
[PublicAPI]
public sealed class TokenResponse
{
    /// <summary>
    /// Gets the issued access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the type of the access token, such as <c>Bearer</c>.
    /// </summary>
    [JsonPropertyName("token_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets the refresh token, if any.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets the ID token, if any.
    /// </summary>
    [JsonPropertyName("id_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdToken { get; init; }

    /// <summary>
    /// Gets the space-delimited scope of the issued token, if it differs from the requested scope.
    /// </summary>
    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the type identifier of the issued token (RFC 8693 §2.2.1).
    /// </summary>
    [JsonPropertyName("issued_token_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssuedTokenType { get; init; }

    /// <summary>
    /// Gets the lifetime of the access token in seconds.
    /// </summary>
    [JsonPropertyName("expires_in")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiresIn { get; init; }

    /// <summary>
    /// Gets or sets the members of the response that have no dedicated property.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
