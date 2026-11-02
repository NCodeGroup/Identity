// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;

/// <summary>
/// Contains the claims about the authenticated subject returned by the <c>UserInfo</c> endpoint
/// (<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse">OpenID Connect Core 5.3.2</see>).
/// The response MUST include a <c>sub</c> member; the default claims handler supplies it and application-provided
/// enrichers contribute the remaining claims.
/// </summary>
[PublicAPI]
public sealed class UserInfoResult
{
    /// <summary>
    /// Gets or sets the claims about the authenticated subject. These are flattened into the top-level JSON response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Claims { get; set; } = [];
}
