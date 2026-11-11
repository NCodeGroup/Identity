// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.Registration;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides extension methods to configure the per-tenant rate-limiting services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the request rate limiter and the rate-limit setting descriptors; the OpenID endpoint group
        /// installs the limiter's endpoint filter. The limiter counts in the
        /// <see cref="NCode.Identity.SharedState.IExpiringCounterSet"/> the composition root registers, so the limits
        /// hold across every replica that shares it.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddRateLimitingServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddMetrics();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<AuthenticationMetrics>();
            serviceCollection.TryAddSingleton<IRequestRateLimiter, DefaultRequestRateLimiter>();
            serviceCollection.TryAddSingleton<
                IRateLimitDescriptorCatalog,
                DefaultRateLimitDescriptorCatalog
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    DefaultRateLimitSettingDescriptorDataSource
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<RateLimitDescriptor>,
                    DefaultRateLimitDescriptorDataSource
                >()
            );

            return builder;
        }
    }
}
