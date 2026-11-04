// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Dispatch;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Registration;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Provides extension methods to configure the services that find the values master keys protect.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Registers the sources of the protected values the family stores (stored secrets and grant payloads), the
        /// announcement of master key changes, and the job that rewraps the values of retired keys.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddSecretProtectionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IProtectedValueSource, PersistedSecretValueSource>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IProtectedValueSource, GrantPayloadValueSource>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IMasterKeyChangeObserver, MasterKeyChangeAnnouncer>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IReplicaBroadcastHandler,
                    MasterKeyChangeBroadcastHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<
                    IDispatchJobHandler<MasterKeyRewrapJob>,
                    MasterKeyRewrapJobHandler
                >()
            );

            return builder;
        }
    }
}
