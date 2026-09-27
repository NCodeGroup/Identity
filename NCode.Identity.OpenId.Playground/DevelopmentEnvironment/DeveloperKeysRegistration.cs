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
using NCode.Identity.OpenId.Authentication.Tenants.Providers;

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
    /// <summary>
    /// Replaces the default static-single tenant provider with one that generates an ephemeral in-memory
    /// <c>RSA</c> signing key, so that token signing and the JWKS endpoint work out-of-the-box for local
    /// development and testing. Must not be used in production.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddEphemeralDeveloperKeys(this IServiceCollection services)
    {
        // Surgically replace only the static-single provider; leave any dynamic tenant providers intact.
        var existing = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IOpenIdTenantProvider) &&
                descriptor.ImplementationType == typeof(DefaultStaticSingleOpenIdTenantProvider))
            .ToList();

        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdTenantProvider,
            EphemeralStaticSingleOpenIdTenantProvider>());

        return services;
    }
}
