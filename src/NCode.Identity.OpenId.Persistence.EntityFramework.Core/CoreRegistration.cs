#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;
using NCode.Identity.OpenId.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the core OpenID persistence slice (its
/// entities and stores) against the Entity Framework persistence framework.
/// </summary>
[PublicAPI]
public static class CoreRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the reusable Entity Framework persistence framework and the core OpenID schema slice: the core
        /// model contributor (which adds the core entities to the shared <see cref="OpenIdDbContext"/>) and the core
        /// stores. Make sure to also register the Entity Framework <see cref="DbContext"/> itself.
        /// </summary>
        /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> to use.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceCollection AddEntityFrameworkCorePersistence<TDbContext>()
            where TDbContext : DbContext
        {
            serviceCollection.AddEntityFrameworkPersistenceServices<TDbContext>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IOpenIdModelContributor, CoreModelContributor>()
            );

            serviceCollection.AddStore<TDbContext, IServerStore, ServerStore>();
            serviceCollection.AddStore<TDbContext, ITenantStore, TenantStore>();
            serviceCollection.AddStore<TDbContext, IClientStore, ClientStore>();
            serviceCollection.AddStore<TDbContext, IGrantStore, GrantStore>();
            serviceCollection.AddStore<TDbContext, IResourceServerStore, ResourceServerStore>();
            serviceCollection.AddStore<TDbContext, IClientGrantStore, ClientGrantStore>();
            serviceCollection.AddStore<TDbContext, IRoleAssignmentStore, RoleAssignmentStore>();
            serviceCollection.AddStore<
                TDbContext,
                IFederatedPrincipalStore,
                FederatedPrincipalStore
            >();
            serviceCollection.AddStore<
                TDbContext,
                IFederatedIdentityStore,
                FederatedIdentityStore
            >();

            return serviceCollection;
        }
    }
}
