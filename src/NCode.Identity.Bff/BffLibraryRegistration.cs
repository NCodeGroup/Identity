// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Secrets;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.Collections;

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
        /// the BFF's frontends take a valid change without a restart. Every key ring configured under
        /// <c>Bff:KeyRings</c> is loaded when the host starts, from PEM files and keys that a key management service
        /// holds, and a keyed <see cref="ISecretKeyCollectionProvider"/>, named after the key ring, returns the loaded
        /// key ring for every key ring configured when this method is called. A keyed provider resolves once the host
        /// starts: resolving it earlier, such as from an eager singleton or an options validator, throws an
        /// <see cref="InvalidOperationException"/> that names the key ring.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the BFF settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        // See ADR-1442.
        // See ADR-1479.
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

            builder.ServiceCollection.TryAddSingleton<BffKeyRings>();
            builder.ServiceCollection.TryAddSingleton<BffRemoteKeyReader>();
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, BffKeyRingLoader>()
            );

            foreach (var child in section.GetSection("KeyRings").GetChildren())
            {
                var name = child.Key;
                builder.ServiceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
                    name,
                    (serviceProvider, _) =>
                        serviceProvider.GetRequiredService<BffKeyRings>().Get(name)
                );
            }

            return builder;
        }
    }
}
