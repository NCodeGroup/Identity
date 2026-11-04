// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Applies a <see cref="TenantKeyRolloverPlan"/> to a tenant's keys in the caller's unit of work: generates, activates,
/// retires and removes keys through the tenant store, and records each change as an audit event. The caller commits
/// the unit of work.
/// </summary>
// See ADR-1404.
internal interface ITenantKeyRollover
{
    /// <summary>
    /// Applies the plan to the tenant's keys.
    /// </summary>
    /// <param name="tenantStore">The tenant store of the caller's unit of work.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="held">The secrets the tenant holds, as the plan was made from.</param>
    /// <param name="plan">The plan to apply.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the secrets the
    /// tenant holds once the plan is applied.</returns>
    ValueTask<IReadOnlyCollection<PersistedSecret>> ApplyAsync(
        ITenantStore tenantStore,
        string tenantId,
        IReadOnlyCollection<PersistedSecret> held,
        TenantKeyRolloverPlan plan,
        CancellationToken cancellationToken
    );
}
