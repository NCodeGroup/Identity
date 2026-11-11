// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Registration;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, <strong>development-only</strong> opt-in that seeds the public (PKCE-only)
/// single-page-application clients and the confidential BFF clients its configuration declares and a deterministic
/// administrator user into the one tenant its configuration names, so a local host can exercise the interactive
/// authorization-code flow and its frontend surfaces end to end. It is intentionally NOT part of the default
/// registrations and must not be used in production.
/// </summary>
[PublicAPI]
public static class DeveloperInteractiveSeedRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the development-only seeder for public single-page-application clients, confidential BFF clients
        /// and an administrator user. Its inputs are read from the <c>DeveloperInteractiveSeed</c> configuration
        /// section, bound strictly; each part is inert unless its inputs are configured (there are no built-in
        /// clients, and the administrator user requires an explicitly configured password), and the section must
        /// name the tenant to seed whenever there is something to seed. Must not be used in production.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the seeder's settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">The <c>DeveloperInteractiveSeed</c> section contains a key that
        /// maps to no option, or configures something to seed without naming the tenant.</exception>
        public IServiceBuilder<IdentityServer> AddDeveloperInteractiveSeed(
            IConfiguration configuration
        )
        {
            var serviceCollection = builder.ServiceCollection;

            var options = new DeveloperInteractiveSeedOptions();
            var section = configuration.GetSection(
                DeveloperInteractiveSeedOptions.DefaultSectionName
            );
            section.BindStrictly(options);

            // A seeder that names no tenant would seed every tenant, including each one created at runtime (ADR-1166).
            var hasSeedData =
                options.SpaClients.Count > 0
                || options.BffClients.Count > 0
                || options.SeedAdminUser;
            if (hasSeedData && string.IsNullOrEmpty(options.TenantId))
            {
                throw new InvalidOperationException(
                    $"{DeveloperInteractiveSeedOptions.DefaultSectionName}:{nameof(options.TenantId)} is required "
                        + "when the section configures clients or an administrator: name the one tenant to seed."
                );
            }

            serviceCollection.AddSingleton(options);
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    DeveloperInteractiveSeedHandler
                >()
            );

            // Project the interactively-authenticated subject's role claim into the access token so the seeded
            // administrator can exercise the role-gated management API during local development.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
                    DeveloperInteractiveSubjectClaimsHandler
                >()
            );

            return builder;
        }
    }
}
