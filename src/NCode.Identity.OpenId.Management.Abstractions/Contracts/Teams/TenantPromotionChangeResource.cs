// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the change a promotion makes to one resource of the target tenant.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionChangeResource
{
    /// <summary>
    /// Gets the type of the resource, such as <c>tenant</c>, <c>resource_server</c>, <c>client</c> or
    /// <c>client_grant</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource in the target tenant.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the action taken on the resource. See <see cref="TenantPromotionActions"/> for the known values.
    /// </summary>
    public required string Action { get; init; }
}
