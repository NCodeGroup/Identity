// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Decides, from the keys a tenant holds and its effective settings, what the rollover changes at one instant: the
/// keys to generate, activate, retire and remove. The seed and the background pass apply one planner, so a tenant's
/// keys converge whichever runs first.
/// </summary>
// See ADR-1404.
internal interface ITenantKeyRolloverPlanner
{
    /// <summary>
    /// Plans the changes that bring the tenant's keys in line with its settings at <paramref name="now"/>.
    /// </summary>
    /// <param name="specifications">The keys every tenant is provisioned with.</param>
    /// <param name="held">The secrets the tenant holds.</param>
    /// <param name="settings">The tenant's effective settings.</param>
    /// <param name="now">The instant to plan at.</param>
    /// <returns>The plan, empty when nothing changes.</returns>
    TenantKeyRolloverPlan Plan(
        IReadOnlyList<TenantKeySpecification> specifications,
        IReadOnlyCollection<PersistedSecret> held,
        IReadOnlySettingCollection settings,
        DateTimeOffset now
    );

    /// <summary>
    /// Determines whether the tenant holds a key that serves, or will serve, the provisioned key: one that satisfies
    /// it and has neither retired nor expired at <paramref name="now"/>.
    /// </summary>
    /// <param name="specification">The provisioned key.</param>
    /// <param name="held">The secrets the tenant holds.</param>
    /// <param name="now">The instant to judge at.</param>
    /// <returns><c>true</c> when a held key serves or will serve the provisioned key; otherwise, <c>false</c>.</returns>
    bool HasServingKey(
        TenantKeySpecification specification,
        IReadOnlyCollection<PersistedSecret> held,
        DateTimeOffset now
    );
}
