// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Contains what a promotion does for one grant of a source client: the grant the target holds, the resource server
/// the grant reaches in the target tenant, and the errors that refuse it.
/// </summary>
// See ADR-1425.
internal sealed class ClientGrantPromotion
{
    /// <summary>
    /// Gets the identifier of the client the grant belongs to.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the grant of the source tenant.
    /// </summary>
    public required PersistedClientGrant Source { get; init; }

    /// <summary>
    /// Gets the grant of the target tenant for the same client and resource server, or <c>null</c> when the
    /// promotion creates it.
    /// </summary>
    public required PersistedClientGrant? Target { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the grant reaches in the target tenant, or <c>null</c> when the
    /// source's resource server is neither promoted nor a system resource server of the target tenant.
    /// </summary>
    public required string? TargetResourceServerId { get; init; }

    /// <summary>
    /// Gets the action the promotion takes, one of the <c>TenantPromotionActions</c> values.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the reasons the promotion refuses the grant.
    /// </summary>
    public required List<string> Errors { get; init; }

    /// <summary>
    /// Gets the identifier the report uses for the grant.
    /// </summary>
    public string ResourceId => $"{ClientId}/{TargetResourceServerId ?? Source.ResourceServerId}";
}
