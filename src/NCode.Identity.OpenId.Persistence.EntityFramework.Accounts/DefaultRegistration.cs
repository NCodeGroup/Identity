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
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;
using NCode.Persistence;
using NCode.Persistence.Stores;
using NCode.Registration;

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
        /// argument, the model contributor that adds the account entities to the shared <c>OpenIdDbContext</c>, and the
        /// default password hasher. The receiver proves the Entity Framework persistence framework is registered.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> proving a local-account store is registered.</returns>
        public IServiceBuilder<LocalAccountLibrary> AddLocalAccountEntityFramework()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = serviceCollection.NewBuilder<LocalAccountLibrary>();

            serviceCollection.AddDefaultPasswordHasher();

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
