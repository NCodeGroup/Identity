// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        /// key that maps to no option fails when the options are first read. It also registers a keyed
        /// <see cref="ISecretKeyCollectionProvider"/> for every key ring configured under <c>Bff:KeyRings</c>, named
        /// after the key ring.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the BFF settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<BffLibrary> AddBffConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(BffDefaults.ConfigurationSectionName);

            builder
                .ServiceCollection.AddOptions<BffOptions>()
                .Configure(options => section.BindStrictly(options));

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
