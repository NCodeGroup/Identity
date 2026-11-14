// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.Registration;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides extension methods to read <see cref="LocalDevelopmentOptions"/> from configuration.
/// </summary>
[PublicAPI]
public static class LocalDevelopmentConfiguration
{
    /// <summary>
    /// Binds the <c>LocalDevelopment</c> configuration section strictly. It is the one binding of the section that both
    /// the registration-time read and the registered options use.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfiguration"/> to read the <c>LocalDevelopment</c> section
    /// from.</param>
    /// <param name="options">The <see cref="LocalDevelopmentOptions"/> to bind the section to.</param>
    internal static void BindSection(
        IConfiguration configuration,
        LocalDevelopmentOptions options
    ) => configuration.GetSection(LocalDevelopmentOptions.DefaultSectionName).BindStrictly(options);

    /// <param name="configuration">The <see cref="IConfiguration"/> to read the local development switch from.</param>
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// Gets a value indicating whether the <c>LocalDevelopment</c> configuration section turns on the local
        /// inner-loop relaxations. The section is read for choices that the host makes at registration time, before the
        /// service provider exists, so it is bound strictly right away. An absent section is <c>false</c>.
        /// </summary>
        /// <returns><c>true</c> when <c>LocalDevelopment:Enabled</c> is <c>true</c>; otherwise, <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The section contains a key that maps to no option, or a value
        /// that is not valid for its option.</exception>
        public bool IsLocalDevelopment()
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var options = new LocalDevelopmentOptions();
            BindSection(configuration, options);

            return options.Enabled;
        }
    }
}
