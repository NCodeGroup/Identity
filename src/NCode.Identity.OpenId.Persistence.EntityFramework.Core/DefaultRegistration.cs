// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the core OpenID persistence slice (its
/// entities and stores) against the Entity Framework persistence framework.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
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
        /// <returns>The <see cref="IServiceBuilder{T}"/> proving the store-manager framework is registered, which the
        /// local-account store slices require.</returns>
        [PublicAPI]
        public IServiceBuilder<StoreManagerLibrary> AddEntityFrameworkCorePersistence<TDbContext>()
            where TDbContext : DbContext
        {
            var newBuilder = serviceCollection.AddEntityFrameworkPersistenceServices<TDbContext>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IOpenIdModelContributor, CoreModelContributor>()
            );

            serviceCollection.AddStore<TDbContext, IPlatformStore, PlatformStore>();
            serviceCollection.AddStore<TDbContext, ITenantStore, TenantStore>();
            serviceCollection.AddStore<TDbContext, ITeamStore, TeamStore>();
            serviceCollection.AddStore<TDbContext, IClientStore, ClientStore>();
            serviceCollection.AddStore<TDbContext, IGrantStore, GrantStore>();
            serviceCollection.AddStore<TDbContext, IConsentStore, ConsentStore>();
            serviceCollection.AddStore<TDbContext, IResourceServerStore, ResourceServerStore>();
            serviceCollection.AddStore<TDbContext, IClientGrantStore, ClientGrantStore>();
            serviceCollection.AddStore<TDbContext, IRoleAssignmentStore, RoleAssignmentStore>();
            serviceCollection.AddStore<TDbContext, ITenantUsageStore, TenantUsageStore>();
            serviceCollection.AddStore<TDbContext, IAuditRecordStore, AuditRecordStore>();
            serviceCollection.AddStore<TDbContext, IMasterKeyStore, MasterKeyStore>();
            serviceCollection.AddStore<TDbContext, IOwnerRootKeyStore, OwnerRootKeyStore>();
            serviceCollection.AddStore<TDbContext, ISecretEnvelopeStore, SecretEnvelopeStore>();
            serviceCollection.AddStore<
                TDbContext,
                IRemoteKeyReferenceStore,
                RemoteKeyReferenceStore
            >();
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

            return newBuilder;
        }
    }
}
