// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Provides extension methods to register the default password hasher.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default PBKDF2 <see cref="IPasswordHasher"/> implementation. A host may substitute another
        /// implementation (such as one backed by ASP.NET Core Identity) by registering its own before this call.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDefaultPasswordHasher()
        {
            serviceCollection.AddOptions();
            serviceCollection.TryAddSingleton<IPasswordHasher, DefaultPasswordHasher>();
            return serviceCollection;
        }
    }
}
