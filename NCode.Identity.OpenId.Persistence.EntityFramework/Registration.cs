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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Stores;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services needed for using Entity Framework with OpenId.
/// </summary>
public static class Registration
{
    /// <summary>
    /// Registers the required services needed for using Entity Framework with OpenId into the provided <see cref="IServiceCollection"/> instance.
    /// Make sure to also register the required services for Entity Framework itself.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> to use.</typeparam>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddEntityFrameworkPersistenceServices<TDbContext>(
        this IServiceCollection serviceCollection)
        where TDbContext : DbContext
    {
        serviceCollection.TryAddSingleton<IdValueGenerator>();
        serviceCollection.TryAddSingleton<UseIdGeneratorConvention>();

        serviceCollection.TryAddSingleton<IStoreManagerFactory, EntityStoreManagerFactory<TDbContext>>();
        serviceCollection.TryAddScoped<IStoreManager, EntityStoreManager<TDbContext>>();

        AddFactoryFunction<TDbContext, ITenantStore, TenantStore>(serviceCollection);
        AddFactoryFunction<TDbContext, IClientStore, ClientStore>(serviceCollection);
        AddFactoryFunction<TDbContext, IGrantStore, GrantStore>(serviceCollection);

        return serviceCollection;
    }

    private static void AddFactoryFunction<TArg, TService, TImplementation>(
        this IServiceCollection serviceCollection)
        where TArg : class
        where TService : class
        where TImplementation : class, TService
    {
        serviceCollection.AddSingleton<Func<TArg, TService>>(
            provider => arg => ActivatorUtilities.CreateInstance<TImplementation>(provider, arg));
    }
}
