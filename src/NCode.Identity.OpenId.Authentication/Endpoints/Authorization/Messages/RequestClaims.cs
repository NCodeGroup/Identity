// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IRequestClaims"/> abstraction.
/// </summary>
public sealed class RequestClaims : IRequestClaims
{
    /// <inheritdoc cref="IRequestClaims.UserInfo" />
    [JsonPropertyName(OpenIdProtocol.Parameters.UserInfo)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestClaimDictionary? UserInfo { get; set; }

    /// <inheritdoc cref="IRequestClaims.IdToken" />
    [JsonPropertyName(OpenIdProtocol.Parameters.IdToken)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestClaimDictionary? IdToken { get; set; }

    /// <summary>
    /// Contains the additional data that is not captured by other properties during serialization.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = [];

    IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.UserInfo => UserInfo;

    IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.IdToken => IdToken;
}
