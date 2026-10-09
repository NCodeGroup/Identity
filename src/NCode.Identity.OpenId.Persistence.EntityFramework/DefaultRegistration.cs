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

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the reusable Entity Framework
/// persistence framework (the store-manager, id generation, and the <see cref="AddStore{TDbContext,TService,TImplementation}"/>
/// helper that slices use to register their stores). Register a schema slice (such as the core OpenID slice) separately
/// to contribute entities and stores.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the reusable Entity Framework persistence framework (the id generator, the id-generator
        /// convention, and the store-manager/unit-of-work plumbing) into the provided <see cref="IServiceCollection"/>.
        /// Make sure to also register the Entity Framework <see cref="DbContext"/> itself and the schema slices whose
        /// stores you need.
        /// </summary>
        /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> to use.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceCollection AddEntityFrameworkPersistenceServices<TDbContext>()
            where TDbContext : DbContext
        {
            serviceCollection.TryAddSingleton<IdValueGenerator>();
            serviceCollection.TryAddSingleton<UseIdGeneratorConvention>();

            serviceCollection.TryAddSingleton<
                IStoreManagerFactory,
                EntityStoreManagerFactory<TDbContext>
            >();
            serviceCollection.TryAddScoped<IStoreManager, EntityStoreManager<TDbContext>>();

            return serviceCollection;
        }

        /// <summary>
        /// Registers a store implementation against the shared <see cref="DbContext"/>. A schema slice calls this for
        /// each of its stores so they share the framework's unit of work; the store is created with the current
        /// <see cref="IStoreProvider"/> and <typeparamref name="TDbContext"/>.
        /// </summary>
        /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> to use.</typeparam>
        /// <typeparam name="TService">The store service contract.</typeparam>
        /// <typeparam name="TImplementation">The store implementation type.</typeparam>
        [PublicAPI]
        public void AddStore<TDbContext, TService, TImplementation>()
            where TDbContext : DbContext
            where TService : class
            where TImplementation : class, TService
        {
            serviceCollection.AddSingleton<Func<IStoreProvider, TDbContext, TService>>(
                serviceProvider =>
                    (storeProvider, dbContext) =>
                        ActivatorUtilities.CreateInstance<TImplementation>(
                            serviceProvider,
                            storeProvider,
                            dbContext
                        )
            );
        }
    }
}
