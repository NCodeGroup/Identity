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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, <strong>conformance-only</strong> opt-in that seeds the confidential clients and the
/// deterministic test user the OpenID Foundation conformance suite drives, into each workload tenant, so a launched
/// host exposes the same identities with no manual API calls. It is intentionally NOT part of the default
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
        /// configuration section; each half is inert unless its inputs are configured (the test user requires an
        /// explicitly configured password). Must not be used in production.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the seeder's settings.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddConformanceSeed(IConfiguration configuration)
        {
            var serviceCollection = builder.ServiceCollection;

            var options = new ConformanceSeedOptions();
            configuration.GetSection(ConformanceSeedOptions.DefaultSectionName).Bind(options);

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
