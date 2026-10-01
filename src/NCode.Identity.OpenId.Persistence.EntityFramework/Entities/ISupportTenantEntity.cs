#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Indicates that an entity supports the <see cref="Tenant"/> navigation property
/// and the <see cref="TenantId"/> foreign key.
/// </summary>
[PublicAPI]
public interface ISupportTenantEntity
{
    /// <summary>
    /// Gets the foreign key for the associated tenant.
    /// </summary>
    long TenantId { get; }

    /// <summary>
    /// Gets the denormalized, normalized (uppercase) natural identifier of the associated tenant, stored on the
    /// entity itself so that tenant-scoping does not require a join to the tenant table. This keeps a tenant-scoped
    /// row self-identifying — the prerequisite for isolating a tenant's data into its own database
    /// (<see href="../../../docs/adr/0024-control-plane-and-per-tenant-planes.md">ADR-0024</see>).
    /// </summary>
    string NormalizedTenantId { get; }

    /// <summary>
    /// Gets the navigation property for the associated tenant.
    /// </summary>
    TenantEntity Tenant { get; }
}
