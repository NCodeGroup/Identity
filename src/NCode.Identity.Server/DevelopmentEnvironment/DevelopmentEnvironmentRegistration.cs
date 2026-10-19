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
using Microsoft.Extensions.Hosting;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Wires every development-only convenience behind a single, self-gating call.
/// </summary>
/// <remarks>
/// Previously a host opted into the developer CORS policy, the developer and conformance seeders, and the developer
/// signing keys as four separate calls, each gated only by its own configuration section. That spread the decision
/// "is this a development host?" across four places in the composition root, and a host that added a fifth
/// convenience had to remember to gate it the same way. Collapsing them means a reviewer reads one line, and the
/// environment check cannot be forgotten for one of them.
/// </remarks>
[PublicAPI]
public static class DevelopmentEnvironmentRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Registers the development-only conveniences — the developer CORS policy, the developer and conformance
        /// seeders, and the developer signing keys — and does nothing at all in a Production environment.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> each convenience reads its own section from.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> that gates the whole block.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        /// <remarks>
        /// Safe to leave wired unconditionally. Each convenience remains individually inert when its configuration
        /// section is absent, so a non-Production host that configures none of them still gets nothing beyond the
        /// ephemeral signing keys that make a zero-setup host work.
        /// <para>
        /// In Production this returns without registering anything, including the signing keys — a Production
        /// deployment supplies its own. That is a second line of defence rather than the primary one:
        /// <c>AddProductionSafety</c> already refuses to start such a deployment, and it reports the problem instead
        /// of silently changing behaviour. This guard matters when a host has not called it.
        /// </para>
        /// </remarks>
        public IServiceBuilder<IdentityServer> AddDevelopmentEnvironment(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            if (hostEnvironment.IsProduction())
                return builder;

            builder.ServiceCollection.AddDeveloperCors(configuration, hostEnvironment);

            // Seeds the public SPA clients and the development administrator its section declares, so the frontend
            // surfaces can sign in. Inert unless its section is configured (the reference host's Development overlay
            // declares its clients).
            builder.AddDeveloperInteractiveSeed(configuration);

            // Seeds the confidential clients and deterministic test user the OpenID Foundation conformance suite
            // drives. Inert unless the Conformance environment overlay supplies its section.
            builder.AddConformanceSeed(configuration);

            // Chooses how signing keys are provided (ADR-0002). Defaults to ephemeral, in-memory keys, which is what
            // makes a freshly-cloned repository run and the integration tests hermetic.
            var developerKeys = configuration.GetDeveloperKeysOptions();
            builder.AddDeveloperKeys(developerKeys.Mode ?? DeveloperKeyMode.Ephemeral);

            return builder;
        }
    }
}
