// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Registration;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to configure the backend-for-frontend (BFF) library.
/// </summary>
[PublicAPI]
public static class BffLibraryRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="BffLibrary"/>.</param>
    extension(IServiceBuilder<BffLibrary> builder)
    {
        /// <summary>
        /// Binds <see cref="BffOptions"/> strictly from the <c>Bff</c> section of <paramref name="configuration"/>, so a
        /// key that maps to no option fails when the options are first read, and follows the section's reloads, so
        /// the BFF's frontends take a valid change without a restart. It also registers a keyed
        /// <see cref="ISecretKeyCollectionProvider"/> for every key ring configured under <c>Bff:KeyRings</c> when it
        /// is called, named after the key ring.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the BFF settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        // See ADR-1442.
        public IServiceBuilder<BffLibrary> AddBffConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(BffDefaults.ConfigurationSectionName);

            builder
                .ServiceCollection.AddOptions<BffOptions>()
                .Configure(options => section.BindStrictly(options));

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IOptionsChangeTokenSource<BffOptions>>(
                    new ConfigurationChangeTokenSource<BffOptions>(
                        Microsoft.Extensions.Options.Options.DefaultName,
                        section
                    )
                )
            );

            foreach (var child in section.GetSection("KeyRings").GetChildren())
            {
                var name = child.Key;
                builder.ServiceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
                    name,
                    (serviceProvider, _) => BffPemKeyRing.Load(serviceProvider, name)
                );
            }

            return builder;
        }
    }
}
