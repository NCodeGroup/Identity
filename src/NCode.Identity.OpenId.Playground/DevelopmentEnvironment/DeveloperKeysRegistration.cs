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
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Tenants;

namespace NCode.Identity.OpenId.Playground.DevelopmentEnvironment;

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
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces the default tenant factory with one that generates an ephemeral in-memory
        /// <c>RSA</c> signing key, so that token signing and the JWKS endpoint work out-of-the-box for local
        /// development and testing. Must not be used in production.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddEphemeralDeveloperKeys()
        {
            // Replace the single tenant factory with one that generates an ephemeral in-memory RSA signing key.
            services.Replace(
                ServiceDescriptor.Singleton<IOpenIdTenantFactory, EphemeralOpenIdTenantFactory>()
            );

            return services;
        }
    }
}
