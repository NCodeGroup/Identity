// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Defines the known values of <see cref="TenantPromotionChangeResource.Action"/>.
/// </summary>
// See ADR-1425.
[PublicAPI]
public static class TenantPromotionActions
{
    /// <summary>
    /// The target tenant has no such resource, so the promotion creates it.
    /// </summary>
    public const string Created = "created";

    /// <summary>
    /// The target tenant's resource differs from the promoted one, so the promotion updates it.
    /// </summary>
    public const string Updated = "updated";

    /// <summary>
    /// The target tenant's resource already holds the promoted configuration, so the promotion leaves it.
    /// </summary>
    public const string Unchanged = "unchanged";
}
