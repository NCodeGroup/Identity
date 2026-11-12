// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Identity.AspNetCore.TransportSecurity;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Registers the Production check that the host serves HTTPS only, with a usable certificate.
/// </summary>
/// <remarks>
/// Safe to leave wired: the check is registered only in the Production environment.
/// </remarks>
// See ADR-1504.
[PublicAPI]
public static class TransportSecurityRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// In the Production environment, makes the host refuse to start unless Kestrel serves only HTTPS endpoints
        /// whose certificate is valid, has a private key, is strong and is not the ASP.NET Core development
        /// certificate. Inert in every other environment.
        /// </summary>
        /// <remarks>
        /// The check runs as the host finishes starting, after Kestrel has bound its endpoints.
        /// </remarks>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> that selects the environment.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddTransportSecurity(IHostEnvironment hostEnvironment)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            if (!hostEnvironment.IsProduction())
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
