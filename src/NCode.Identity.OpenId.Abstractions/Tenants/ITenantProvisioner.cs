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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Brings tenants into existence and into a seeded state, independent of how they are later resolved. It creates the
/// tenants configuration defines (the root tenant, and a static strategy's own tenant) on demand, and it seeds any tenant
/// by fanning out <see cref="SeedTenantCommand"/> to every registered handler within a single unit of work, after
/// ensuring the root tenant exists and is seeded. Both operations are idempotent, so the server seeds every tenant it
/// materializes and the management API seeds every tenant it creates.
/// </summary>
[PublicAPI]
public interface ITenantProvisioner
{
    /// <summary>
    /// Ensures the tenant with the specified identifier exists, creating it when it does not with its strategy's default
    /// issuer (<see cref="ITenantResolver.GetDefaultIssuerAsync"/>), judged by
    /// <see cref="ITenantResolver.ValidateTenantAsync"/> like any other tenant written. It does not seed the tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to create.</param>
    /// <param name="displayName">The display name applied when the tenant is created; an existing tenant keeps its own.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the existing or newly
    /// created <see cref="PersistedTenant"/>.</returns>
    /// <exception cref="InvalidOperationException">The tenant does not exist and cannot be created at its default
    /// address.</exception>
    ValueTask<PersistedTenant> EnsureCreatedAsync(
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Brings the existing tenant with the specified identifier into a seeded state: it ensures the root tenant exists
    /// and is seeded first when the tenant is not itself the root tenant, then runs every seed contributor for the tenant
    /// within a single unit of work. A seed whose commit loses to a concurrent seed of the same tenant runs again in a
    /// fresh unit of work, so concurrent seeds converge on one seeded state.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to seed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the seeded
    /// <see cref="PersistedTenant"/>.</returns>
    ValueTask<PersistedTenant> SeedAsync(string tenantId, CancellationToken cancellationToken);
}
