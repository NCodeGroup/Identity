// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration;

namespace NCode.Identity.Claims;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the claims services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default claims service and claims serializer, minting the <see cref="ClaimsLibrary"/>
        /// marker that the libraries built on claims require.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the claims library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<ClaimsLibrary> AddClaimsLibrary()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            var newBuilder = serviceCollection.NewBuilder<ClaimsLibrary>();

            serviceCollection.TryAddSingleton<IClaimsService, DefaultClaimsService>();
            serviceCollection.TryAddSingleton<IClaimsSerializer>(DefaultClaimsSerializer.Singleton);

            return newBuilder;
        }
    }
}
