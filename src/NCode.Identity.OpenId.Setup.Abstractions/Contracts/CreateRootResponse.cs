// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the response to creating the root administrator.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateRootResponse
{
    /// <summary>
    /// Gets the identifier of the control-plane tenant that holds the root administrator.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the root administrator's principal.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the root administrator's username.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the <c>client_id</c> of the management console's client that setup registered, or <c>null</c> when setup
    /// registered none.
    /// </summary>
    public string? ManagementConsoleClientId { get; init; }

    /// <summary>
    /// Gets the issuer of the control-plane tenant. It is computed from the control-plane tenant, not from the request.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the address of the control-plane tenant's OpenID Connect discovery document. It is computed from the
    /// control-plane tenant, not from the request.
    /// </summary>
    public required string DiscoveryUrl { get; init; }

    /// <summary>
    /// Gets the address of the management API, which sits at the origin of the control-plane tenant's issuer. It is
    /// computed from the control-plane tenant, not from the request.
    /// </summary>
    public required string ManagementApiUrl { get; init; }

    /// <summary>
    /// Gets the address of the API reference page at the origin of the control-plane tenant's issuer, or <c>null</c>
    /// when the host serves none. It is computed from the control-plane tenant, not from the request.
    /// </summary>
    public string? ApiReferenceUrl { get; init; }

    /// <summary>
    /// Gets the base URL of the management console that setup registered, or <c>null</c> when setup registered none.
    /// It is the base URL of the console's registration, which the control-plane tenant holds.
    /// </summary>
    public string? ManagementConsoleBaseUrl { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
