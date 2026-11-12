// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;
using NCode.Identity.Persistence;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Provides extension methods to register the generic Entity Framework local-account reference implementation.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The builder proving the store-manager framework
    /// (<c>AddEntityFrameworkCorePersistence</c>) is registered.</param>
    extension(IServiceBuilder<StoreManagerLibrary> builder)
    {
        /// <summary>
        /// Registers the generic Entity Framework <see cref="ILocalAccountStore"/> together with the
        /// <see cref="LocalAccountLibrary"/> marker that the opt-in local-account capabilities require as a compile-time
        /// argument, and the model contributor that adds the account entities to the shared <c>OpenIdDbContext</c>. The
        /// store hashes and verifies passwords with the <see cref="IPasswordHasher"/> that the host registers, for
        /// example with <c>AddDefaultPasswordHasher</c> from the <c>NCode.Identity.OpenId.Accounts</c> package. The
        /// receiver proves the Entity Framework persistence framework is registered.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> proving a local-account store is registered.</returns>
        public IServiceBuilder<LocalAccountLibrary> AddLocalAccountEntityFramework()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = serviceCollection.NewBuilder<LocalAccountLibrary>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IOpenIdModelContributor, LocalAccountModelContributor>()
            );

            serviceCollection.AddSingleton<
                Func<IStoreProvider, OpenIdDbContext, ILocalAccountStore>
            >(serviceProvider =>
                (storeProvider, dbContext) =>
                    ActivatorUtilities.CreateInstance<LocalAccountStore>(
                        serviceProvider,
                        storeProvider,
                        dbContext
                    )
            );

            return newBuilder;
        }
    }
}
