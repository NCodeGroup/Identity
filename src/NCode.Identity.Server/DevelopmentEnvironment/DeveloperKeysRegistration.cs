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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, <strong>development-only</strong> opt-in that makes the OpenID server fully
/// runnable without any configured or persisted secret keys by generating ephemeral in-memory signing
/// keys.
/// </summary>
/// <remarks>
/// This is intentionally NOT part of the default registrations: production deployments must supply a
/// stable, securely-managed signing key. See ADR-0002 for the rationale.
/// </remarks>
[PublicAPI]
public static class DeveloperKeysRegistration
{
    private const string DeveloperApplicationName = "NCode.Identity.Server";

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Applies the development-only signing-key setup selected by <paramref name="mode"/>:
        /// <see cref="DeveloperKeyMode.Ephemeral"/> uses in-memory keys, while
        /// <see cref="DeveloperKeyMode.PersistentSigningKey"/> seeds a persisted key and stores the data-protection
        /// key ring under <c>App_Data/dp-keys</c> (relative to the content root) so it survives restarts. Must not be
        /// used in production.
        /// </summary>
        /// <param name="mode">The <see cref="DeveloperKeyMode"/> that selects which setup to apply.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> whose content root anchors the key-ring
        /// directory when <paramref name="mode"/> is <see cref="DeveloperKeyMode.PersistentSigningKey"/>.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDeveloperKeys(
            DeveloperKeyMode mode,
            IHostEnvironment hostEnvironment
        )
        {
            if (mode is DeveloperKeyMode.PersistentSigningKey)
            {
                var keyRingDirectory = new DirectoryInfo(
                    Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "dp-keys")
                );
                return serviceCollection.AddDeveloperSigningKey(keyRingDirectory);
            }

            return serviceCollection.AddEphemeralDeveloperKeys();
        }

        /// <summary>
        /// Replaces the default tenant factory with one that generates an ephemeral in-memory
        /// <c>RSA</c> signing key, so that token signing and the JWKS endpoint work out-of-the-box for local
        /// development and testing. Must not be used in production.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddEphemeralDeveloperKeys()
        {
            // Replace the single tenant factory with one that generates an ephemeral in-memory RSA signing key.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<IOpenIdTenantFactory, EphemeralOpenIdTenantFactory>()
            );

            return serviceCollection;
        }

        /// <summary>
        /// Replaces the default tenant factory with one that seeds a persisted <c>RSA</c> signing key through the
        /// normal persistence path (so the runtime, JWKS, and the management API share a single source of truth),
        /// and persists the data-protection key ring to <paramref name="keyRingDirectory"/> so the seeded secret
        /// survives restarts. Intended for local development only; must not be used in production.
        /// </summary>
        /// <param name="keyRingDirectory">The directory where the data-protection key ring is persisted. It should
        /// be excluded from source control.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDeveloperSigningKey(DirectoryInfo keyRingDirectory)
        {
            keyRingDirectory.Create();

            // Persist the key ring so persisted secrets can be unprotected across restarts. On Windows the ring is
            // encrypted at rest with DPAPI; on other platforms it is stored unprotected (development only).
            var dataProtection = serviceCollection
                .AddDataProtection()
                .PersistKeysToFileSystem(keyRingDirectory)
                .SetApplicationName(DeveloperApplicationName);

            if (OperatingSystem.IsWindows())
            {
                dataProtection.ProtectKeysWithDpapi();
            }

            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    IOpenIdTenantFactory,
                    DeveloperSigningKeyOpenIdTenantFactory
                >()
            );

            return serviceCollection;
        }
    }
}
