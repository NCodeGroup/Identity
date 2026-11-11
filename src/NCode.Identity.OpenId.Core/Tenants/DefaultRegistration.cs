// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Core.ResourceServers;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Identity.Registration;
using NCode.Identity.SharedState;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides extension methods to configure services for tenant resolution.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the shared tenant-resolution services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddTenantResolutionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IAmbientTenantAccessor,
                DefaultAmbientTenantAccessor
            >();

            serviceCollection.TryAddSingleton<ITenantResolver, DefaultTenantResolver>();

            // A tenant-resolution configuration that cannot work stops the host when it starts (ADR-1137). The shared
            // validator checks the strategy-independent members; each strategy's validator is registered beside it.
            serviceCollection.AddOptions<TenantResolutionOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<TenantResolutionOptions>,
                    TenantResolutionOptionsValidator
                >()
            );

            // The materialized tenant (settings + secrets, merged platform->team->tenant) and its cache (ADR-1036).
            serviceCollection.AddMemoryCache();
            serviceCollection.TryAddSingleton<IOpenIdTenantCache, DefaultOpenIdTenantCache>();

            // The path-based strategy binds the tenant route via TemplateBinderFactory (from AddRouting).
            serviceCollection.AddRouting();
            serviceCollection.TryAddSingleton<IOpenIdTenantFactory, DefaultOpenIdTenantFactory>();

            // Tenants are provisioned and seeded through one mediator-driven pipeline: the provisioner opens a single
            // unit of work and fans SeedTenantCommand out to every seed handler (ADR-1045).
            serviceCollection.AddMediator();
            serviceCollection.TryAddScoped<ITenantProvisioner, DefaultTenantProvisioner>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    OpenIdIdentityResourceServerSeedHandler
                >()
            );

            // Every tenant is provisioned with a persisted key for each configured algorithm it holds no usable key for,
            // on the same seed path, and the configured keys are validated when the host starts (ADR-1169). The keys
            // roll over: one planner decides what the seed and the background pass change, and one rollover applies
            // it (ADR-1404).
            serviceCollection.TryAddSingleton<ITenantKeyPolicy, DefaultTenantKeyPolicy>();
            serviceCollection.TryAddSingleton<
                ITenantKeyRolloverPlanner,
                DefaultTenantKeyRolloverPlanner
            >();
            serviceCollection.TryAddSingleton<ITenantKeyRollover, DefaultTenantKeyRollover>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    TenantKeySeedHandler
                >()
            );
            serviceCollection.AddHostedService<TenantKeyRolloverService>();

            serviceCollection.TryAddSingleton(TimeProvider.System);

            // A committed change to a tenant evicts it from this replica's cache and seeds it again, so a key its new
            // settings require is provisioned at once rather than when the tenant is next materialized (ADR-1169).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<TenantChangedCommand>,
                    TenantChangedHandler
                >()
            );

            // A tenant change announced on the replica broadcast channel evicts the tenant from this replica's cache,
            // so every replica serves the change rather than only the one that made it (ADR-1205).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IReplicaBroadcastHandler,
                    TenantChangeBroadcastHandler
                >()
            );

            // A change to a team's effective settings seeds every tenant of the team that lacks a key its settings use
            // (ADR-1412).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ReconcileTeamSettingsCommand>,
                    TenantKeyReconciliationHandler
                >()
            );

            serviceCollection.AddOptions<OpenIdOptions>().ValidateOnStart();

            // The token lifetime settings are a bound collection, so they start empty and take their defaults only
            // when configuration names none; the validators run after this, so they check the names in force.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IPostConfigureOptions<OpenIdOptions>,
                    TenantKeyRolloverOptionsDefaults
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdOptions>,
                    TenantKeyProvisioningOptionsValidator
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdOptions>,
                    TenantKeyRolloverOptionsValidator
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, StaticSingleTenantStrategy>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<TenantResolutionOptions>,
                    StaticSingleTenantOptionsValidator
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, DynamicByHostTenantStrategy>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<TenantResolutionOptions>,
                    DynamicByHostTenantOptionsValidator
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ITenantStrategy, DynamicByPathTenantStrategy>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<TenantResolutionOptions>,
                    DynamicByPathTenantOptionsValidator
                >()
            );

            return builder;
        }
    }
}
