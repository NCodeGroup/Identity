// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Provides extension methods to compose the management console.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Composes the management console in one call: the backend for frontend (BFF) that serves it, configured from
        /// the <c>Bff</c> section of <paramref name="configuration"/>, and the console's own services, configured from
        /// the <c>ManagementConsole</c> section. The configuration must define the <c>mgmt</c> BFF frontend.
        /// </summary>
        /// <param name="configuration">The configuration whose sections configure the BFF and the console.</param>
        /// <returns>The <see cref="IManagementConsoleBuilder"/> instance for additional configuration.</returns>
        public IManagementConsoleBuilder AddManagementConsole(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            return new ManagementConsoleBuilder(serviceCollection, configuration);
        }
    }
}
