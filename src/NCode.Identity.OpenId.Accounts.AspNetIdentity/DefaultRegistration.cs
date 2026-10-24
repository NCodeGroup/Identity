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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Persistence;
using NCode.Persistence.Stores;
using NCode.Registration;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Provides extension methods to register the ASP.NET Core Identity <see cref="ILocalAccountStore"/> adapter.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The builder proving the store-manager framework (the Entity Framework persistence) is
    /// registered — the adapter registers a factory the store manager resolves.</param>
    extension(IServiceBuilder<StoreManagerLibrary> builder)
    {
        /// <summary>
        /// Registers an <see cref="ILocalAccountStore"/> that adapts the ASP.NET Core Identity
        /// <c>UserManager&lt;TUser&gt;</c> for the specified user type, together with the <see cref="LocalAccountLibrary"/>
        /// marker that the opt-in local-account capabilities require as a compile-time argument. The host is responsible
        /// for registering ASP.NET Core Identity (<c>AddIdentityCore&lt;TUser&gt;</c> or <c>AddIdentity</c>) and its
        /// stores. It wraps the host's <c>IPasswordHasher&lt;TUser&gt;</c> registration, made before or after this call,
        /// in <see cref="NormalizingPasswordHasher{TUser}"/>, so the adapter and the host's own ASP.NET Core Identity
        /// pages hash and verify a password's NFC form; a hasher that the host registers after this call must be wrapped
        /// by the host.
        /// </summary>
        /// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
        /// <returns>The <see cref="IServiceBuilder{T}"/> proving a local-account store is registered.</returns>
        public IServiceBuilder<LocalAccountLibrary> AddAspNetIdentityLocalAccountStore<TUser>()
            where TUser : IdentityUser, new()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = serviceCollection.NewBuilder<LocalAccountLibrary>();

            // See ADR-0239.
            serviceCollection.TryAddSingleton<AspNetIdentityDummyPasswordHash<TUser>>();

            // See ADR-0243.
            AddNormalizingPasswordHasher<TUser>(serviceCollection);

            // The store is backing-store-agnostic, so it registers the DbContext-less factory the store manager falls
            // back to; the scoped UserManager is resolved per operation from a fresh scope.
            serviceCollection.AddSingleton<Func<IStoreProvider, ILocalAccountStore>>(
                serviceProvider =>
                    storeProvider =>
                        ActivatorUtilities.CreateInstance<AspNetIdentityLocalAccountStore<TUser>>(
                            serviceProvider,
                            storeProvider
                        )
            );

            return newBuilder;
        }
    }

    // Replaces the last registration of the host's hasher in place with one that wraps it, so the order of this call
    // and of ASP.NET Core Identity's registration, which keeps a registration that exists, does not matter. See
    // ADR-0243.
    private static void AddNormalizingPasswordHasher<TUser>(IServiceCollection serviceCollection)
        where TUser : class
    {
        for (var index = serviceCollection.Count - 1; index >= 0; index--)
        {
            var descriptor = serviceCollection[index];
            if (
                descriptor.ServiceType != typeof(IPasswordHasher<TUser>)
                || descriptor.IsKeyedService
            )
            {
                continue;
            }

            serviceCollection[index] = ServiceDescriptor.Describe(
                typeof(IPasswordHasher<TUser>),
                serviceProvider =>
                {
                    var innerHasher = CreateInnerHasher<TUser>(serviceProvider, descriptor);
                    return Wrap(innerHasher);
                },
                descriptor.Lifetime
            );
            return;
        }

        serviceCollection.AddScoped<IPasswordHasher<TUser>>(serviceProvider =>
        {
            var innerHasher = ActivatorUtilities.CreateInstance<PasswordHasher<TUser>>(
                serviceProvider
            );
            return Wrap(innerHasher);
        });
    }

    private static IPasswordHasher<TUser> CreateInnerHasher<TUser>(
        IServiceProvider serviceProvider,
        ServiceDescriptor descriptor
    )
        where TUser : class
    {
        if (descriptor.ImplementationInstance is { } implementationInstance)
        {
            return (IPasswordHasher<TUser>)implementationInstance;
        }

        if (descriptor.ImplementationFactory is { } implementationFactory)
        {
            var created = implementationFactory(serviceProvider);
            return (IPasswordHasher<TUser>)created;
        }

        if (descriptor.ImplementationType is { } implementationType)
        {
            var instance = ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
            return (IPasswordHasher<TUser>)instance;
        }

        throw new InvalidOperationException(
            $"The registration of IPasswordHasher<{typeof(TUser).Name}> has no instance, factory or implementation type."
        );
    }

    private static NormalizingPasswordHasher<TUser> Wrap<TUser>(IPasswordHasher<TUser> innerHasher)
        where TUser : class =>
        innerHasher as NormalizingPasswordHasher<TUser>
        ?? new NormalizingPasswordHasher<TUser>(innerHasher);
}
