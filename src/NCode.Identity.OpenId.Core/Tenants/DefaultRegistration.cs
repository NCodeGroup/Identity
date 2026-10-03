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
using NCode.Identity.OpenId.Core.ResourceServers;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Registration;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides extension methods to configure services for tenant resolution.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the shared tenant-resolution services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddTenantResolutionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IAmbientTenantAccessor,
                DefaultAmbientTenantAccessor
            >();

            serviceCollection.TryAddSingleton<ITenantResolver, DefaultTenantResolver>();

            // The materialized tenant (settings + secrets, merged server->tenant) and its cache (ADR-0036).
            serviceCollection.AddMemoryCache();
            serviceCollection.TryAddSingleton<IOpenIdTenantCache, DefaultOpenIdTenantCache>();
            serviceCollection.TryAddSingleton<IOpenIdTenantFactory, DefaultOpenIdTenantFactory>();

            serviceCollection.TryAddSingleton<
                ISystemResourceServerSeeder,
                DefaultSystemResourceServerSeeder
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ISystemResourceServerProvider,
                    DefaultOpenIdIdentityResourceServerProvider
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, StaticSingleTenantStrategy>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, DynamicByHostTenantStrategy>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, DynamicByPathTenantStrategy>()
            );

            return builder;
        }
    }
}
