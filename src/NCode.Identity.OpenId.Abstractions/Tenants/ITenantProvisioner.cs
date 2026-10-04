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
/// Ensures a tenant exists and is seeded, independent of how the tenant is later resolved. Provisioning the root
/// tenant first and then the target tenant, it runs the seed contributors (via <see cref="SeedTenantCommand"/>)
/// within a single unit of work. It is idempotent, so it may be invoked whenever a tenant must be brought into a
/// seeded state — on the lazy resolution path of a static strategy, or by management tooling that creates tenants.
/// </summary>
[PublicAPI]
public interface ITenantProvisioner
{
    /// <summary>
    /// Ensures the tenant with the specified identifier exists and is seeded, provisioning the root tenant first when
    /// the target tenant is not itself the root tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to provision.</param>
    /// <param name="displayName">The display name applied when the tenant is created for the first time.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the provisioned
    /// <see cref="PersistedTenant"/>.</returns>
    ValueTask<PersistedTenant> ProvisionAsync(
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    );
}
