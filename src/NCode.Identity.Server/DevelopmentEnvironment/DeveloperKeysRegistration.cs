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
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides the <strong>development-only</strong> signing-key setups: ephemeral in-memory keys for hermetic test hosts,
/// and persisted keys that heal themselves when the development key ring is lost.
/// </summary>
/// <remarks>
/// Neither is needed for a server to have keys: every tenant is provisioned with persisted keys by default. These
/// setups change only what development needs — keys with no database footprint, or keys that are replaced rather than
/// failing the tenant when the data-protection key ring that protected them is gone. Neither may be used in production.
/// </remarks>
[PublicAPI]
public static class DeveloperKeysRegistration
{
    extension(IServiceBuilder<IdentityServer> builder)
    {
        /// <summary>
        /// Applies the development-only signing-key setup selected by <paramref name="mode"/>:
        /// <see cref="DeveloperKeyMode.Ephemeral"/> uses in-memory keys, while
        /// <see cref="DeveloperKeyMode.PersistentSigningKey"/> uses the persisted keys every tenant is provisioned with
        /// and prunes those the data-protection key ring can no longer unprotect. <see cref="DeveloperKeyMode.None"/>
        /// leaves the persisted keys exactly as production uses them. The host owns where the data-protection key ring
        /// is stored (the OpenID host persists it in its database). Must not be used in production.
        /// </summary>
        /// <param name="mode">The <see cref="DeveloperKeyMode"/> that selects which setup to apply.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddDeveloperKeys(DeveloperKeyMode mode)
        {
            return mode switch
            {
                DeveloperKeyMode.None => builder,
                DeveloperKeyMode.PersistentSigningKey => builder.AddPersistentDeveloperKeys(),
                _ => builder.AddEphemeralDeveloperKeys(),
            };
        }

        /// <summary>
        /// Replaces the default tenant factory with one that generates an ephemeral in-memory
        /// <c>RSA</c> signing key, so that token signing and the JWKS endpoint work out-of-the-box for local
        /// development and testing, and turns off tenant key provisioning, since the tenant's persisted keys are never
        /// read. Must not be used in production.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddEphemeralDeveloperKeys()
        {
            var serviceCollection = builder.ServiceCollection;

            // Replace the single tenant factory with one that generates an ephemeral in-memory RSA signing key.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<IOpenIdTenantFactory, EphemeralOpenIdTenantFactory>()
            );

            // The ephemeral factory never reads a tenant's persisted keys, so provisioning them would only spend key
            // generation and storage on keys nothing uses (ADR-0169).
            serviceCollection.PostConfigure<OpenIdOptions>(options =>
                options.Tenant.KeyProvisioning.Enabled = false
            );

            return builder;
        }

        /// <summary>
        /// Keeps the persisted keys every tenant is provisioned with — so the runtime, the JWKS endpoint, and the
        /// management API share a single source of truth that survives restarts — and adds development-only self-healing:
        /// a tenant secret the data-protection key ring can no longer unprotect is removed when the tenant is seeded, and
        /// key provisioning generates a fresh key in its place. The host is responsible for configuring data-protection
        /// key-ring persistence so the keys can be unprotected across restarts (the OpenID host persists the ring in its
        /// database). Intended for local development only; must not be used in production, where an undecryptable key
        /// fails the tenant instead of being discarded.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<IdentityServer> AddPersistentDeveloperKeys()
        {
            var serviceCollection = builder.ServiceCollection;

            // The pruner runs on the tenant seed path ahead of key provisioning, which then replaces what it removed
            // (ADR-0021, ADR-0045, ADR-0169).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    DeveloperSecretPruningSeedHandler
                >()
            );

            return builder;
        }
    }
}
