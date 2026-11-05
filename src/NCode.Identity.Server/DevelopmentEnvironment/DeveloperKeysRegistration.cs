// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides the <strong>development-only</strong> signing-key convenience: persisted keys that heal themselves when their
/// master key, or the root key that wraps it, is lost or changed.
/// </summary>
/// <remarks>
/// It is not needed for a server to have keys: every tenant is provisioned with persisted keys. It changes
/// only what development needs: keys are replaced rather than failing the tenant when the master key that protected them
/// can no longer be unwrapped. It may not be used in production.
/// </remarks>
// See ADR-1169.
[PublicAPI]
public static class DeveloperKeysRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Adds development-only self-healing to the persisted keys every tenant is provisioned with, which keeps the
        /// runtime, the JWKS endpoint, and the management API on a single source of truth that survives restarts: a
        /// tenant secret that can no longer be opened, because its master key was purged or the root key that wrapped
        /// the master key changed, is removed when the tenant is seeded, and key provisioning generates a fresh key in
        /// its place. The persisted keys are envelopes under the tenant's master key, so the host is responsible for
        /// supplying a root key that stays the same across restarts for as long as its database does. Intended for local
        /// development only; must not be used in production, where an undecryptable key fails the tenant instead of
        /// being discarded.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddDeveloperKeyPruning()
        {
            var serviceCollection = builder.ServiceCollection;

            // The pruner runs on the tenant seed path ahead of key provisioning, which then replaces what it removed
            // (ADR-1021, ADR-1045, ADR-1169).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    DeveloperSecretPruningSeedHandler
                >()
            );

            serviceCollection.TryAddSingleton<DeveloperMasterKeyReset>();

            return builder;
        }
    }
}
