// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IRequestClaim"/> abstraction.
/// </summary>
public sealed class RequestClaim : IRequestClaim
{
    /// <inheritdoc />
    [JsonPropertyName(OpenIdProtocol.Parameters.Essential)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Essential { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdProtocol.Parameters.Value)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdProtocol.Parameters.Values)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Values { get; set; }

    /// <summary>
    /// Contains the additional data that is not captured by other properties during serialization.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = [];
}
