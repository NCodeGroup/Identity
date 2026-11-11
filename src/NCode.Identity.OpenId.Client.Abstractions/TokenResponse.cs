// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Identity.Json;

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
    [JsonPropertyName(OpenIdProtocol.Parameters.AccessToken)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the type of the access token, such as <c>Bearer</c>.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.TokenType)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets the refresh token, if any.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.RefreshToken)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets the ID token, if any.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.IdToken)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdToken { get; init; }

    /// <summary>
    /// Gets the space-delimited scope of the issued token, if it differs from the requested scope.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.Scope)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the type identifier of the issued token (RFC 8693 §2.2.1).
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.IssuedTokenType)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssuedTokenType { get; init; }

    /// <summary>
    /// Gets the lifetime of the access token in seconds. A whole number is read in any JSON number form, such as
    /// <c>60</c> or <c>60.0</c>; a number with a non-zero fraction fails deserialization.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.ExpiresIn)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(WholeInt32JsonConverter))]
    public int? ExpiresIn { get; init; }

    /// <summary>
    /// Gets or sets the members of the response that have no dedicated property.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// Gets the seconds until the refresh token's rotation family expires, from the de-facto
    /// <c>refresh_token_expires_in</c> member, or <see langword="null"/> when the provider sends none. The member is
    /// advisory, so a value that is not a positive whole number is ignored rather than failing the response. It is read
    /// from <see cref="ExtensionData"/>.
    /// </summary>
    [JsonIgnore]
    public int? RefreshTokenExpiresIn =>
        ExtensionData is { } extensionData
        && extensionData.TryGetValue(
            OpenIdProtocol.Parameters.RefreshTokenExpiresIn,
            out var refreshTokenExpiresIn
        )
        && refreshTokenExpiresIn.TryGetWholeInt32(out var seconds)
        && seconds > 0
            ? seconds
            : null;
}
