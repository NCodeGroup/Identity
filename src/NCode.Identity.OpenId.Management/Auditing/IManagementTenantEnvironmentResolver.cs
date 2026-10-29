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

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Resolves the effective <c>environment_type</c> of the tenant a management audit event names, so control-plane
/// records carry <c>tenant_environment_type</c> as protocol records do.
/// </summary>
// See ADR-0289.
internal interface IManagementTenantEnvironmentResolver
{
    /// <summary>
    /// Gets the effective environment type of a tenant, or <see langword="null"/> when the tenant does not exist or its
    /// settings cannot be read. A type remembered for the request with
    /// <see cref="ManagementTenantEnvironment.Remember"/> is returned without reading the store.
    /// </summary>
    /// <param name="httpContext">The current request, which memoizes the resolved types.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective environment type, or <see langword="null"/>.</returns>
    ValueTask<string?> GetEnvironmentTypeAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    );
}
