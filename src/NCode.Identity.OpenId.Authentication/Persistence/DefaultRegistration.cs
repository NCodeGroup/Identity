// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides extension methods to configure the persisted-grant services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the persisted-grant service, its schema-versioned payload serializer, and the hosted service
        /// that removes the grants that ended before their retention.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddPersistenceServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IPersistedPayloadSerializer,
                DefaultPersistedPayloadSerializer
            >();
            serviceCollection.TryAddSingleton<
                IGrantPayloadProtector,
                DefaultGrantPayloadProtector
            >();
            serviceCollection.TryAddSingleton<
                IPersistedGrantService,
                DefaultPersistedGrantService
            >();

            serviceCollection.AddOptions<GrantRetentionOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<GrantRetentionOptions>,
                    GrantRetentionOptionsValidator
                >()
            );
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddHostedService<GrantPruneService>();

            return builder;
        }
    }
}
