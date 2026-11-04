// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Identifies the deployment plane a tenant belongs to, so that seeding contributors can target the control plane
/// and the workload plane consistently.
/// </summary>
[PublicAPI]
public enum TenantPlane
{
    /// <summary>
    /// The workload plane, which holds a tenant's own clients, resource servers, scopes, and grants. This is the
    /// plane of every tenant other than the root tenant.
    /// </summary>
    Workload = 0,

    /// <summary>
    /// The control plane, which administers the platform, teams and tenants. This is the plane of the root tenant,
    /// whose identifier is <see cref="RootTenantOptions.TenantId"/>.
    /// </summary>
    Root = 1,
}
