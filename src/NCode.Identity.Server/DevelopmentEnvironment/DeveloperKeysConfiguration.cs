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
using Microsoft.Extensions.Configuration;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Registration;

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
        /// read when the host composes its services, because the mode decides which services are registered, so it is
        /// bound strictly and checked right away.
        /// </summary>
        /// <returns>The bound <see cref="DeveloperKeysOptions"/>.</returns>
        /// <exception cref="InvalidOperationException">The section contains a key that maps to no option, or a mode
        /// that names no <see cref="DeveloperKeyMode"/>. A mode the server cannot honor must not fall back to a default
        /// silently: a misspelled <c>PersistentSigningKey</c> would otherwise regenerate the signing key on every
        /// restart.</exception>
        // See ADR-0137.
        public DeveloperKeysOptions GetDeveloperKeysOptions()
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var options = new DeveloperKeysOptions();
            var section = configuration.GetSection(DeveloperKeysOptions.DefaultSectionName);
            section.BindStrictly(options);

            if (options.Mode is { } mode && !Enum.IsDefined(mode))
            {
                var modes = string.Join(", ", Enum.GetNames<DeveloperKeyMode>());
                throw new InvalidOperationException(
                    $"The configuration value '{section.Path}:{nameof(options.Mode)}' is '{mode}', which is not a "
                        + $"developer key mode. Use one of: {modes}."
                );
            }

            return options;
        }
    }
}
