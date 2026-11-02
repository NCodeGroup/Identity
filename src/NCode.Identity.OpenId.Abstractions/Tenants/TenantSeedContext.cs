// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Carries the shared state for seeding a tenant: the tenant being seeded, the plane it belongs to, and the unit of work
/// that every seed contributor enlists in so the whole seed commits atomically. A tenant is seeded every time the server
/// materializes it, so contributors are idempotent: they check for their data before writing it.
/// </summary>
[PublicAPI]
public sealed class TenantSeedContext
{
    /// <summary>
    /// Gets the tenant being seeded.
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
}
