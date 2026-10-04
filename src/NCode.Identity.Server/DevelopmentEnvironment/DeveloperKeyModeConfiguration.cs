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

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods to resolve the <see cref="DeveloperKeyMode"/> from configuration.
/// </summary>
[PublicAPI]
public static class DeveloperKeyModeConfiguration
{
    private const string DeveloperKeyModeConfigurationKey = "DeveloperKeys:Mode";

    /// <param name="configuration">The <see cref="IConfiguration"/> to read the developer-key mode from.</param>
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// Reads the <see cref="DeveloperKeyMode"/> from the <c>DeveloperKeys:Mode</c> configuration value, defaulting
        /// to <see cref="DeveloperKeyMode.Ephemeral"/> when the value is absent or unrecognized.
        /// </summary>
        /// <returns>The resolved <see cref="DeveloperKeyMode"/>.</returns>
        public DeveloperKeyMode GetDeveloperKeyMode() =>
            Enum.TryParse<DeveloperKeyMode>(
                configuration[DeveloperKeyModeConfigurationKey],
                ignoreCase: true,
                out var mode
            )
                ? mode
                : DeveloperKeyMode.Ephemeral;
    }
}
