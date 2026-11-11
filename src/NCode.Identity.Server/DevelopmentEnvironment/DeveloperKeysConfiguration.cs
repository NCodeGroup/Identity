// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Identity.Registration;
using NCode.Identity.Server.DevelopmentEnvironment;

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods to read <see cref="DeveloperKeysOptions"/> from configuration.
/// </summary>
[PublicAPI]
public static class DeveloperKeysConfiguration
{
    /// <param name="configuration">The <see cref="IConfiguration"/> to read the developer-key options from.</param>
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// Binds <see cref="DeveloperKeysOptions"/> from the <c>DeveloperKeys</c> configuration section. The section is
        /// read when the host composes its services, because the option decides which services are registered, so it is
        /// bound strictly and checked right away.
        /// </summary>
        /// <returns>The bound <see cref="DeveloperKeysOptions"/>.</returns>
        /// <exception cref="InvalidOperationException">The section contains a key that maps to no option, or a value
        /// that is not valid for its option.</exception>
        // See ADR-1137.
        public DeveloperKeysOptions GetDeveloperKeysOptions()
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var options = new DeveloperKeysOptions();
            var section = configuration.GetSection(DeveloperKeysOptions.DefaultSectionName);
            section.BindStrictly(options);

            return options;
        }
    }
}
