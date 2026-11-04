// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a resource server's effective (merged) settings — the team's effective settings
/// merged with the tenant's and the resource server's persisted overrides — that the authorization server resolves at
/// runtime.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResourceServerEffectiveSettingsResource
    : ISupportResourceServerId,
        ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportResourceServerId.ResourceServerId"/>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the read-only merged (team effective settings + tenant overrides + resource server overrides) flat JSON
    /// settings object the authorization server resolves at runtime.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
