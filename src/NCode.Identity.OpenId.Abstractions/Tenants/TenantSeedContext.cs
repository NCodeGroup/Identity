// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Carries the shared state for seeding a tenant: the tenant being seeded, the plane it belongs to, the unit of work
/// that every seed contributor enlists in so the whole seed commits atomically, and the steps that follow its commit.
/// A tenant is seeded every time the server materializes it, so contributors are idempotent: they check for their data
/// before writing it.
/// </summary>
[PublicAPI]
public sealed class TenantSeedContext
{
    private List<Func<CancellationToken, ValueTask>> CommittedSteps { get; } = [];

    /// <summary>
    /// Gets the tenant being seeded: the seed's working copy, read before the contributors run. A contributor that
    /// changes the tenant's settings or secrets updates this instance, so the contributors that follow observe the
    /// change before the seed commits; the provisioner returns the tenant as the seed committed it.
    /// </summary>
    public required PersistedTenant Tenant { get; init; }

    /// <summary>
    /// Gets the deployment plane the tenant belongs to. Contributors use this to target the control plane (the root
    /// tenant) or the workload plane consistently, instead of each re-deriving it.
    /// </summary>
    public required TenantPlane Plane { get; init; }

    /// <summary>
    /// Gets the unit of work that all seed contributors share. Contributors obtain their stores from it and must not
    /// save it; the provisioning handler commits once after every contributor has run.
    /// </summary>
    public required IStoreManager StoreManager { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant being seeded.
    /// </summary>
    public string TenantId => Tenant.TenantId;

    /// <summary>
    /// Gets the steps that run, in the order they were added, once the seed's unit of work has committed: an effect
    /// that must follow the committed seed and never describe one a lost race discarded, such as the audit event of a
    /// key the seed generated. A seed that loses its commit to a concurrent seed runs again with a fresh context, so
    /// the steps of the discarded attempt never run.
    /// </summary>
    public IReadOnlyList<Func<CancellationToken, ValueTask>> PostCommitSteps => CommittedSteps;

    /// <summary>
    /// Adds a step that runs once the seed's unit of work has committed. The step runs on a token of its own, bounded
    /// by a timeout, so the request that materialized the tenant cannot leave the seed committed without its effect.
    /// </summary>
    /// <param name="step">The step to run after the commit.</param>
    public void RunAfterCommit(Func<CancellationToken, ValueTask> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        CommittedSteps.Add(step);
    }
}
