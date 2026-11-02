// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Provides extension methods to register the endpoint-group routing infrastructure.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the <see cref="IEndpointTreeRouteBuilder"/> that materializes the registered endpoint-group
        /// hierarchy onto the HTTP request pipeline.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddEndpointGroupRouting()
        {
            serviceCollection.TryAddSingleton<
                IEndpointTreeRouteBuilder,
                DefaultEndpointTreeRouteBuilder
            >();
            return serviceCollection;
        }
    }
}
