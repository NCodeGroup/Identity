// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Registration;
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
    /// <param name="builder">The <see cref="OpenIdManagementLibrary"/> builder this capability extends.</param>
    extension(IServiceBuilder<OpenIdManagementLibrary> builder)
    {
        /// <summary>
        /// Registers the local-account management endpoints (nested under the tenant-plane group
        /// <c>/api/tenants/{tenantId}</c>) and the default <see cref="ILocalAccountValidator"/>.
        /// </summary>
        /// <param name="localAccountStore">The local-account store builder — the compile-time proof that a
        /// <c>ILocalAccountStore</c> is registered, so this capability cannot be added without one.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> for method chaining.</returns>
        public IServiceBuilder<OpenIdManagementLibrary> AddLocalAccountManagement(
            IServiceBuilder<LocalAccountLibrary> localAccountStore
        )
        {
            ArgumentNullException.ThrowIfNull(localAccountStore);

            var serviceCollection = builder.ServiceCollection;

            // The handlers' XML documentation describes their OpenAPI operations only in a project that calls AddOpenApi.
            serviceCollection.AddOpenApi();

            // Attach the endpoint to the existing tenant-plane group by registering it keyed under the group's name
            // (ADR-1042); the management route tree resolves keyed providers per group when it maps routes.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.KeyedSingleton<IEndpointProvider, LocalAccountApiEndpointHandler>(
                    ManagementEndpointGroups.Tenant
                )
            );

            serviceCollection.TryAddSingleton<
                ILocalAccountValidator,
                DefaultLocalAccountValidator
            >();

            return builder;
        }
    }
}
