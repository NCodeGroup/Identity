// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides extension methods to configure services for teams.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the team services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddTeamServices()
        {
            builder.ServiceCollection.TryAddScoped<ITeamProvisioner, DefaultTeamProvisioner>();

            builder.ServiceCollection.TryAddSingleton<
                IOpenIdTeamFactory,
                DefaultOpenIdTeamFactory
            >();
            builder.ServiceCollection.TryAddSingleton<IOpenIdTeamCache, DefaultOpenIdTeamCache>();
            builder.ServiceCollection.TryAddSingleton<
                IOpenIdTeamProvider,
                DefaultOpenIdTeamProvider
            >();

            // The settings that govern a team are contributed as descriptors.
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    TeamSettingDescriptorDataSource
                >()
            );

            // A team change evicts the team and its tenants from this replica's caches and announces the change, and the
            // announcement evicts them from every other replica's (ADR-1403).
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<TeamChangedCommand>,
                    TeamChangedHandler
                >()
            );
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IReplicaBroadcastHandler, TeamChangeBroadcastHandler>()
            );

            return builder;
        }
    }
}
