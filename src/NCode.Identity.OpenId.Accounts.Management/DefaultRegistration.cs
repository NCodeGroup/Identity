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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Provides extension methods to register the local-account management capability: the tenant-scoped management
/// endpoints and the validator that back the configured <c>ILocalAccountStore</c>. This opt-in package plugs into the
/// base <c>NCode.Identity.OpenId.Management</c> slice; the base slice has no knowledge of local accounts.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the local-account management endpoints (nested under the tenant-plane group
        /// <c>/api/tenants/{tenantId}</c>) and the default <see cref="ILocalAccountValidator"/>. The endpoints are only
        /// functional when the host has also registered an <c>ILocalAccountStore</c> (for example, the generic Entity
        /// Framework reference or the ASP.NET Core Identity adapter); without one they report local accounts as
        /// unsupported. Call alongside <c>AddOpenIdManagementLibrary()</c>.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddLocalAccountManagement()
        {
            // Attach the endpoint to the existing tenant-plane group by registering it keyed under the group's name
            // (ADR-0042); the management route tree resolves keyed providers per group when it maps routes.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.KeyedSingleton<IEndpointProvider, LocalAccountApiEndpointHandler>(
                    ManagementEndpointGroups.Tenant
                )
            );

            serviceCollection.TryAddSingleton<
                ILocalAccountValidator,
                DefaultLocalAccountValidator
            >();

            return serviceCollection;
        }
    }
}
