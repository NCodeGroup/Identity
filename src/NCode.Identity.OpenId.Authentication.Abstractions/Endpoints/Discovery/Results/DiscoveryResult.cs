// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Results;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization server metadata discovery response.
/// </summary>
[PublicAPI]
public sealed class DiscoveryResult
{
    /// <summary>
    /// Gets the issuer identifier.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets or sets the additional properties for an <c>OAuth</c> or<c>OpenID Connect</c> response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Metadata { get; set; } = [];
}
