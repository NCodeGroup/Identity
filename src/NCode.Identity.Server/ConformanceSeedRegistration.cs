// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Registration;
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, <strong>conformance-only</strong> opt-in that seeds the confidential clients and the
/// deterministic test user the OpenID Foundation conformance suite drives, into the one tenant its configuration names,
/// so a launched host exposes the same identities with no manual API calls. It is intentionally NOT part of the default
/// registrations and must not be used in production.
/// </summary>
[PublicAPI]
public static class ConformanceSeedRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the conformance-only seeder for the confidential clients and the deterministic test user the
        /// OpenID Foundation conformance suite drives. Its inputs are read from the <c>ConformanceSeed</c>
        /// configuration section, bound strictly; each half is inert unless its inputs are configured (the test user
        /// requires an explicitly configured password), and the section must name the tenant to seed whenever there
        /// is something to seed. Must not be used in production.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the seeder's settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">The <c>ConformanceSeed</c> section contains a key that maps to
        /// no option, or configures something to seed without naming the tenant.</exception>
        public IServiceBuilder<IdentityServer> AddConformanceSeed(IConfiguration configuration)
        {
            var serviceCollection = builder.ServiceCollection;

            var options = new ConformanceSeedOptions();
            var section = configuration.GetSection(ConformanceSeedOptions.DefaultSectionName);
            section.BindStrictly(options);

            // A seeder that names no tenant would seed every tenant, including each one created at runtime (ADR-1166).
            var hasSeedData = options.Clients.Count > 0 || options.SeedTestUser;
            if (hasSeedData && string.IsNullOrEmpty(options.TenantId))
            {
                throw new InvalidOperationException(
                    $"{ConformanceSeedOptions.DefaultSectionName}:{nameof(options.TenantId)} is required when the "
                        + "section configures clients or a test user: name the one tenant to seed."
                );
            }

            serviceCollection.AddSingleton(options);
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    ConformanceSeedHandler
                >()
            );

            return builder;
        }
    }
}
