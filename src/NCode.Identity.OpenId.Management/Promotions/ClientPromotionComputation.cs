// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides the outcome of computing the clients a promotion carries from the source tenant to the target tenant.
/// </summary>
internal sealed class ClientPromotionComputation
{
    /// <summary>
    /// Gets the promotion of each source client that the host does not manage.
    /// </summary>
    public List<ClientPromotion> Promotions { get; } = [];

    /// <summary>
    /// Gets the identifiers of the source clients that the host manages and the promotion leaves out.
    /// </summary>
    public List<string> HostManagedSourceClientIds { get; } = [];

    /// <summary>
    /// Gets or sets the effective settings of the target tenant's team, or <see langword="null"/> when they have not
    /// been resolved.
    /// </summary>
    public IReadOnlySettingCollection? TeamSettings { get; set; }

    /// <summary>
    /// Gets or sets the effective settings the target tenant has once the promoted tenant document applies, or
    /// <see langword="null"/> when they cannot be resolved.
    /// </summary>
    public IReadOnlySettingCollection? NewTenantSettings { get; set; }
}
