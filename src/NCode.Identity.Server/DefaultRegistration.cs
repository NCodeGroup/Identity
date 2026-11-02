// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

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
        /// <returns>The <see cref="IIdentityServerBuilder"/> instance for additional configuration.</returns>
        public IIdentityServerBuilder AddIdentityServer()
        {
            return new IdentityServerBuilder(serviceCollection);
        }
    }
}
