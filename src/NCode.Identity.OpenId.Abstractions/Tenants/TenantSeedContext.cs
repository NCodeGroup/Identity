#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Carries the shared state for seeding a tenant: the tenant being seeded, the plane it belongs to, whether it was
/// just provisioned, and the unit of work that every seed contributor enlists in so the whole seed commits atomically.
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
    /// Gets a value indicating whether the tenant row was created during this provisioning pass (as opposed to
    /// already existing). Contributors may use this to distinguish first-time seeding from reconciliation.
    /// </summary>
    public required bool IsNewlyProvisioned { get; init; }

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
