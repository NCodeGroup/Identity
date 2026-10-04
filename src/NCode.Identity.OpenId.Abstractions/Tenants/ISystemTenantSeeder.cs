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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contributes additional seeding to a resolved tenant, on the same lazy, resolve-time path that provisions tenants and
/// seeds the reserved system resource servers (ADR-0031). Implementations are collected and invoked whenever a tenant is
/// resolved — not only when it is first provisioned — so they may run more than once; each is responsible for its own
/// idempotency and unit of work, and should act only on the tenant it targets (for example, only a workload tenant, or
/// skip the control-plane root tenant).
/// </summary>
[PublicAPI]
public interface ISystemTenantSeeder
{
    /// <summary>
    /// Seeds this contributor's data into the specified freshly-provisioned tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant being provisioned.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SeedAsync(string tenantId, CancellationToken cancellationToken);
}
