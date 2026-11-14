// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods to configure services and handlers for Identity Server.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Configures services and handlers for Identity Server.
        /// </summary>
        /// <remarks>
        /// It registers a <see cref="LocalDevelopmentMode"/> that is off, so every consumer resolves one and the strict
        /// posture holds; the host's <c>AddLocalDevelopmentOptions</c> call replaces it with the mode its configuration
        /// selects.
        /// </remarks>
        /// <returns>The <see cref="IIdentityServerBuilder"/> instance for additional configuration.</returns>
        public IIdentityServerBuilder AddIdentityServer()
        {
            serviceCollection.TryAddSingleton(new LocalDevelopmentMode(false));
            return new IdentityServerBuilder(serviceCollection);
        }
    }
}
