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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, <strong>development-only</strong> opt-in that makes the OpenID server fully
/// runnable without any configured or persisted secret keys by generating ephemeral in-memory signing
/// keys.
/// </summary>
/// <remarks>
/// This is intentionally NOT part of the default registrations: production deployments must supply a
/// stable, securely-managed signing key.
/// </remarks>
[PublicAPI]
public static class DeveloperKeysRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Applies the development-only signing-key setup selected by <paramref name="mode"/>:
        /// <see cref="DeveloperKeyMode.Ephemeral"/> uses in-memory keys, while
        /// <see cref="DeveloperKeyMode.PersistentSigningKey"/> seeds a persisted key through the normal persistence
        /// path so it survives restarts. The host owns where the data-protection key ring is stored (the OpenID host
        /// persists it in its dev database). Must not be used in production.
        /// </summary>
        /// <param name="mode">The <see cref="DeveloperKeyMode"/> that selects which setup to apply.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddDeveloperKeys(DeveloperKeyMode mode)
        {
            return mode switch
            {
                DeveloperKeyMode.None => builder,
                DeveloperKeyMode.PersistentSigningKey => builder.AddDeveloperSigningKey(),
                _ => builder.AddEphemeralDeveloperKeys(),
            };
        }

        /// <summary>
        /// Replaces the default tenant factory with one that generates an ephemeral in-memory
        /// <c>RSA</c> signing key, so that token signing and the JWKS endpoint work out-of-the-box for local
        /// development and testing. Must not be used in production.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddEphemeralDeveloperKeys()
        {
            var serviceCollection = builder.ServiceCollection;

            // Replace the single tenant factory with one that generates an ephemeral in-memory RSA signing key.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<IOpenIdTenantFactory, EphemeralOpenIdTenantFactory>()
            );

            return builder;
        }

        /// <summary>
        /// Replaces the default tenant factory with one that seeds a persisted <c>RSA</c> signing key through the
        /// normal persistence path (so the runtime, JWKS, and the management API share a single source of truth).
        /// The host is responsible for configuring data-protection key-ring persistence so the seeded secret can be
        /// unprotected across restarts (the OpenID host persists the ring in its dev database). Intended for
        /// local development only; must not be used in production.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddDeveloperSigningKey()
        {
            var serviceCollection = builder.ServiceCollection;

            // The production tenant factory reads secrets from the store; this development-only seed handler ensures a
            // persisted RSA signing key exists (and heals an undecryptable one) on the tenant seed path (ADR-0045).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    DeveloperSigningKeySeedHandler
                >()
            );

            return builder;
        }
    }
}
