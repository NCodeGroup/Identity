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

using IdGen;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Servers;

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
        public IServiceBuilder<OpenIdCoreLibrary> AddServerServices()
        {
            var serviceCollection = builder.ServiceCollection;

            // DefaultOpenIdServerFactory and the persistence stores mint entity ids via IIdGenerator<long>. Register a
            // single-node default (generator id 0); multi-node hosts override it with AddIdGen(nodeId) to keep ids unique.
            serviceCollection.TryAddSingleton<IIdGenerator<long>>(new IdGenerator(0));

            serviceCollection.TryAddSingleton<IOpenIdServerFactory, DefaultOpenIdServerFactory>();
            serviceCollection.TryAddSingleton<IOpenIdServerProvider, DefaultOpenIdServerProvider>();

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IMaterializedServerRegistry,
                DefaultMaterializedServerRegistry
            >();

            // A committed server settings change is applied on this replica before the write returns and then announced
            // to every replica (ADR-0219).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ServerSettingsChangedCommand>,
                    ServerSettingsChangedHandler
                >()
            );

            // A server settings change announced on the replica broadcast channel makes every replica read the committed
            // settings again, so each serves the change rather than only the one that made it (ADR-0219).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IReplicaBroadcastHandler,
                    ServerSettingsChangeBroadcastHandler
                >()
            );

            // Each replica reconciles every tenant and client with the server's effective settings whenever they change,
            // one replica per change (ADR-0219).
            serviceCollection.TryAddSingleton<
                IServerSettingsReconciler,
                DefaultServerSettingsReconciler
            >();
            serviceCollection.AddHostedService<ServerSettingsReconciliationService>();

            return builder;
        }
    }
}
