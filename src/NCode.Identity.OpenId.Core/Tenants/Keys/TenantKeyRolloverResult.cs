// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// The outcome of applying a <see cref="TenantKeyRolloverPlan"/> to a tenant's keys in the caller's unit of work: the
/// keys the tenant holds once the plan is applied, and the audit event of each change, which the caller publishes once
/// the unit of work has committed, so the audit trail never records a change a lost race discarded.
/// </summary>
// See ADR-1404.
internal sealed class TenantKeyRolloverResult
{
    /// <summary>
    /// Gets the secrets the tenant holds once the plan is applied.
    /// </summary>
    public required IReadOnlyCollection<PersistedSecret> Secrets { get; init; }

    /// <summary>
    /// Gets the audit event of each change the plan made, in the order the changes were made.
    /// </summary>
    public required IReadOnlyList<TenantKeyAuditEvent> AuditEvents { get; init; }
}
