// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides extension methods to configure the services that promote one tenant's configuration into another.
/// </summary>
// See ADR-1425.
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the promotion services and the built-in contributors of the tenant's settings, resource servers,
        /// clients and client grants.
        /// </summary>
        public void AddTenantPromotionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IPromotedSettingsComposer,
                DefaultPromotedSettingsComposer
            >();
            serviceCollection.TryAddSingleton<ITenantPromoter, DefaultTenantPromoter>();
            serviceCollection.TryAddSingleton<
                IHostManagedClientChecker,
                DefaultHostManagedClientChecker
            >();
            serviceCollection.TryAddSingleton<
                IClientKeySourceChecker,
                DefaultClientKeySourceChecker
            >();
            serviceCollection.TryAddSingleton<IClientJwksUriChecker, DefaultClientJwksUriChecker>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITenantPromotionContributor,
                    TenantSettingsPromotionContributor
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITenantPromotionContributor,
                    ResourceServerPromotionContributor
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITenantPromotionContributor,
                    ClientPromotionContributor
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ITenantPromotionContributor,
                    ClientGrantPromotionContributor
                >()
            );
        }
    }
}
