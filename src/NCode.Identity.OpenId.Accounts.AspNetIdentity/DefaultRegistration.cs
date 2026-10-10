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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Provides extension methods to register the ASP.NET Core Identity <see cref="ILocalAccountStore"/> adapter.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers an <see cref="ILocalAccountStore"/> that adapts the ASP.NET Core Identity
        /// <c>UserManager&lt;TUser&gt;</c> for the specified user type, together with the capability marker that enables
        /// the local-account surfaces. The host is responsible for registering ASP.NET Core Identity
        /// (<c>AddIdentityCore&lt;TUser&gt;</c> or <c>AddIdentity</c>) and its stores.
        /// </summary>
        /// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddAspNetIdentityLocalAccountStore<TUser>()
            where TUser : IdentityUser, new()
        {
            serviceCollection.TryAddSingleton<LocalAccountFeature>();

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

            return serviceCollection;
        }
    }
}
