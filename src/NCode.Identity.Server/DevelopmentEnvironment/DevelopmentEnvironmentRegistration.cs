// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Identity.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Wires every development-only convenience behind a single call.
/// </summary>
/// <remarks>
/// A host opts into the developer CORS policy, the developer and conformance seeders, and the developer key pruning
/// with one call. Each convenience does nothing unless its configuration section is present, and
/// <c>AddProductionSafety</c> is the one place that refuses each of those sections in Production, so this
/// registration never decides the environment.
/// </remarks>
// See ADR-1313.
[PublicAPI]
public static class DevelopmentEnvironmentRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the development-only conveniences: the developer CORS policy, the developer and conformance
        /// seeders, and the developer key pruning. Every one does nothing unless its section is configured, and
        /// <c>AddProductionSafety</c> refuses each configured section in Production.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> each convenience reads its own section from.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <remarks>
        /// Safe to leave wired unconditionally: a host that configures none of the sections gets nothing from it.
        /// </remarks>
        // See ADR-1313.
        public IServiceBuilder<IdentityServer> AddDevelopmentEnvironment(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);

            builder.ServiceCollection.AddDeveloperCors(configuration);

            // Seeds the public SPA clients and the development administrator its section declares, so the frontend
            // surfaces can sign in. Inert unless its section is configured (the reference host's Development overlay
            // declares its clients).
            builder.AddDeveloperInteractiveSeed(configuration);

            // Seeds the confidential clients and deterministic test user the OpenID Foundation conformance suite
            // drives. Inert unless the Conformance environment overlay supplies its section.
            builder.AddConformanceSeed(configuration);

            // Chooses whether signing keys that can no longer be read are pruned (ADR-1313). Every tenant signs with the
            // keys provisioning persists through the store (ADR-1169); the opt-in only lets a lost root key heal.
            if (configuration.GetDeveloperKeysOptions().PruneUnreadableKeys)
            {
                builder.AddDeveloperKeyPruning();
            }

            return builder;
        }
    }
}
