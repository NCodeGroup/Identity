#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Tenants.Providers;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register core tenant services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultTenantRegistration
{
    /// <summary>
    /// Registers core tenant services and handlers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddTenantServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddMemoryCache();

        serviceCollection.TryAddSingleton<
            IOpenIdTenantCache,
            DefaultOpenIdTenantCache>();

        serviceCollection.TryAddSingleton<
            IOpenIdTenantFactory,
            DefaultOpenIdTenantFactory>();

        serviceCollection.TryAddSingleton<
            IOpenIdTenantProviderSelector,
            DefaultOpenIdTenantProviderSelector>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdTenantProvider,
            DefaultStaticSingleOpenIdTenantProvider>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdTenantProvider,
            DefaultDynamicByHostOpenIdTenantProvider>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdTenantProvider,
            DefaultDynamicByPathOpenIdTenantProvider>());

        return serviceCollection;
    }
}
