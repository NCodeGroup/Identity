// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore.Options;

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
        /// It registers <see cref="LocalDevelopmentOptions"/> so every consumer resolves them; the host binds the
        /// <c>LocalDevelopment</c> section by calling <c>AddDeploymentSafety</c>, and the switch is off until it does.
        /// </remarks>
        /// <returns>The <see cref="IIdentityServerBuilder"/> instance for additional configuration.</returns>
        public IIdentityServerBuilder AddIdentityServer()
        {
            serviceCollection.AddOptions<LocalDevelopmentOptions>();
            return new IdentityServerBuilder(serviceCollection);
        }
    }
}
