// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Management.Clients;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Registration;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Wires every development-only convenience behind a single call.
/// </summary>
/// <remarks>
/// A host opts into the developer CORS policy, the bootstrap administrator client, the developer and conformance
/// seeders, and the developer key pruning with one call. Each convenience does nothing unless its configuration
/// section is present, and <c>AddDeploymentSafety</c> is the one place that refuses each of those sections in a
/// deployment, so this registration never decides the environment.
/// </remarks>
// See ADR-1370.
[PublicAPI]
public static class DevelopmentEnvironmentRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the development-only conveniences: the developer CORS policy, the bootstrap administrator client,
        /// the developer and conformance seeders, and the developer key pruning. Every one does nothing unless its
        /// section is configured, and <c>AddDeploymentSafety</c> refuses each configured section in a deployment, a
        /// host without <c>LocalDevelopment:Enabled</c>.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> each convenience reads its own section from.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <remarks>
        /// Safe to leave wired unconditionally: a host that configures none of the sections gets nothing from it.
        /// </remarks>
        // See ADR-1370.
        public IServiceBuilder<IdentityServer> AddDevelopmentEnvironment(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddDeveloperCors(configuration);

            // The bootstrap administrator client's management tokens are stamped with the GlobalAdmin role at issuance
            // (ADR-1044); the handler is a no-op unless a bootstrap client id is configured.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetAccessTokenPayloadClaimsCommand>,
                    BootstrapAdminAccessTokenClaimsHandler
                >()
            );

            // The bootstrap administrator client's secret and authority come from host configuration, so the management
            // API refuses every change to it (ADR-1417).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostManagedClientPolicy,
                    BootstrapAdminHostManagedClientPolicy
                >()
            );

            // The bootstrap administrator client is seeded into the one tenant its options name, on the same tenant
            // seed fan-out that seeds the system resource servers (ADR-1166); inert unless a bootstrap client is
            // configured, and that configuration must name the tenant.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    BootstrapAdminSeedHandler
                >()
            );

            // The bootstrap administrator credential is sourced from its own top-level section so it can be supplied
            // out of band (for example, environment variables); absent it, the client stays inert (ADR-1044).
            var bootstrapAdminSection = configuration.GetSection(
                BootstrapAdminOptions.DefaultSectionName
            );
            serviceCollection
                .AddOptions<BootstrapAdminOptions>()
                .Configure(options => bootstrapAdminSection.BindStrictly(options))
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<BootstrapAdminOptions>,
                    BootstrapAdminOptionsValidator
                >()
            );

            // Seeds the public SPA clients and the development administrator its section declares, so the frontend
            // surfaces can sign in. Inert unless its section is configured (the reference host's Development overlay
            // declares its clients).
            builder.AddDeveloperInteractiveSeed(configuration);

            // Seeds the confidential clients and deterministic test user the OpenID Foundation conformance suite
            // drives. Inert unless the Conformance environment overlay supplies its section.
            builder.AddConformanceSeed(configuration);

            // Chooses whether signing keys that can no longer be read are pruned (ADR-1370). Every tenant signs with the
            // keys provisioning persists through the store (ADR-1169); the opt-in only lets a lost root key heal.
            if (configuration.GetDeveloperKeysOptions().PruneUnreadableKeys)
            {
                builder.AddDeveloperKeyPruning();
            }

            return builder;
        }
    }
}
