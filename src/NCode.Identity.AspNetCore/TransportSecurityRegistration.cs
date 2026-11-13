// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Identity.AspNetCore.TransportSecurity;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Registers the check that the host serves HTTPS only, with a usable certificate.
/// </summary>
/// <remarks>
/// Safe to leave wired: the check is registered unless <c>LocalDevelopment:Enabled</c> is set.
/// </remarks>
// See ADR-1504.
[PublicAPI]
public static class TransportSecurityRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Makes the host refuse to start unless Kestrel serves only HTTPS endpoints whose certificate is valid, has a
        /// private key, is strong and is not the ASP.NET Core development certificate. Inert when
        /// <c>LocalDevelopment:Enabled</c> is set.
        /// </summary>
        /// <remarks>
        /// The check runs as the host finishes starting, after Kestrel has bound its endpoints. The method also
        /// registers the <c>LocalDevelopment</c> switch.
        /// </remarks>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> read for the <c>LocalDevelopment</c> section.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddTransportSecurity(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            serviceCollection.AddLocalDevelopmentOptions(configuration);

            if (configuration.IsLocalDevelopment())
            {
                return serviceCollection;
            }

            serviceCollection.TryAddSingleton<TransportCertificateProbe>();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, TransportSecurityStartupCheck>()
            );

            return serviceCollection;
        }
    }
}
