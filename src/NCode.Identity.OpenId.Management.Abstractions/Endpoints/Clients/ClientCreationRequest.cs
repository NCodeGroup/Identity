// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Describes a client for <see cref="IClientCreator"/> to validate and stage.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientCreationRequest
{
    /// <summary>
    /// Gets the identifier of the OpenID Tenant that owns the client.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the requested and stable <c>client_id</c>: 1 to 255 unreserved URI characters.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client is disabled.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client acts for, or <c>null</c> when the client acts for none.
    /// </summary>
    public string? ResourceServerId { get; init; }

    /// <summary>
    /// Gets the resource type of the node that owns the client, or <c>null</c> unless the client is a machine client.
    /// A machine client is limited to the <c>client_credentials</c> grant.
    /// </summary>
    public string? OwningNodeType { get; init; }

    /// <summary>
    /// Gets the resource identifier of the node that owns the client, or <c>null</c> unless the client is a machine
    /// client.
    /// </summary>
    public string? OwningNodeId { get; init; }

    /// <summary>
    /// Gets the client's settings document.
    /// </summary>
    public required JsonElement Settings { get; init; }

    /// <summary>
    /// Gets the JSON Web Key Set the client registers by value, or <c>null</c> when it registers none. A client that
    /// registers keys by value does not name a <c>jwks_uri</c>.
    /// </summary>
    public JsonElement? Jwks { get; init; }
}
