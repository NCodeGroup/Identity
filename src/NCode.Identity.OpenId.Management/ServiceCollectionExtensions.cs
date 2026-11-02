// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register OpenID management services.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the specified <see cref="IAuthorizationHandler"/> implementation as a singleton.
        /// </summary>
        /// <typeparam name="THandler">The <see cref="IAuthorizationHandler"/> implementation type to register.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddAuthorizationHandler<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler
        >()
            where THandler : class, IAuthorizationHandler
        {
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IAuthorizationHandler, THandler>()
            );
            return serviceCollection;
        }
    }
}
