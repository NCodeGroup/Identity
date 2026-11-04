// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides extension methods to configure services for the OpenID server.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the OpenID server services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddPlatformServices()
        {
            var serviceCollection = builder.ServiceCollection;

            // DefaultOpenIdPlatformFactory and the persistence stores mint entity ids via IIdGenerator<long>. Register a
            // single-node default (generator id 0); multi-node hosts override it with AddIdGen(nodeId) to keep ids unique.
            serviceCollection.TryAddSingleton<IIdGenerator<long>>(new IdGenerator(0));

            serviceCollection.TryAddSingleton<
                IOpenIdPlatformFactory,
                DefaultOpenIdPlatformFactory
            >();
            serviceCollection.TryAddSingleton<
                IOpenIdPlatformProvider,
                DefaultOpenIdPlatformProvider
            >();

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IMaterializedPlatformRegistry,
                DefaultMaterializedPlatformRegistry
            >();

            // Background work in another package waits for the server through the public accessor (ADR-1223).
            serviceCollection.TryAddSingleton<IMaterializedPlatformAccessor>(serviceProvider =>
                serviceProvider.GetRequiredService<IMaterializedPlatformRegistry>()
            );

            // A committed server settings change is applied on this replica before the write returns and then announced
            // to every replica (ADR-1219).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<PlatformSettingsChangedCommand>,
                    PlatformSettingsChangedHandler
                >()
            );

            // A server settings change announced on the replica broadcast channel makes every replica read the committed
            // settings again, so each serves the change rather than only the one that made it (ADR-1219).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IReplicaBroadcastHandler,
                    PlatformSettingsChangeBroadcastHandler
                >()
            );

            // A server or tenant interval that a timer refuses stops the host when it starts instead of failing the
            // first time a timer is created (ADR-1221).
            serviceCollection.AddOptions<OpenIdOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdOptions>,
                    OpenIdIntervalsOptionsValidator
                >()
            );

            return builder;
        }
    }
}
