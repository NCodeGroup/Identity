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

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Wires every development-only convenience behind a single call.
/// </summary>
/// <remarks>
/// A host opts into the developer CORS policy, the developer and conformance seeders, and the developer key pruning
/// with one call. Each convenience does nothing unless its configuration section is present, and
/// <c>AddProductionSafety</c> is the one place that refuses each of those sections in Production (ADR-1313), so this
/// registration never decides the environment.
/// </remarks>
[PublicAPI]
public static class DevelopmentEnvironmentRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the development-only conveniences: the developer CORS policy, the developer and conformance
        /// seeders, and the developer key pruning. Every one does nothing unless its section is configured, and
        /// <c>AddProductionSafety</c> refuses each configured section in Production (ADR-1313).
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> each convenience reads its own section from.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <remarks>
        /// Safe to leave wired unconditionally: a host that configures none of the sections gets nothing from it.
        /// </remarks>
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
