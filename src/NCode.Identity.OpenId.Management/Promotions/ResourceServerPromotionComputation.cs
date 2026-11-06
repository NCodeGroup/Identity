// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides the outcome of computing the resource servers a promotion carries from the source tenant to the target
/// tenant.
/// </summary>
internal sealed class ResourceServerPromotionComputation
{
    /// <summary>
    /// Gets the promotion of each non-system source resource server.
    /// </summary>
    public List<ResourceServerPromotion> Promotions { get; } = [];

    /// <summary>
    /// Gets the target resource server identifier of each source resource server, keyed by the source identifier and
    /// including the system resource servers the target tenant holds.
    /// </summary>
    public Dictionary<string, string> TargetIdentifiers { get; } = new(StringComparer.Ordinal);
}
