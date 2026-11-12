// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.AspNetCore.ProxyClientCertificate;
using NCode.Identity.Registration;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Registers the requirement that every request, except the liveness and readiness probes, arrives on a connection
/// that carries the reverse proxy's client certificate. Apply it with <c>UseProxyClientCertificate</c> as the first
/// middleware and with <c>RequestProxyClientCertificate</c> on Kestrel's HTTPS endpoints.
/// </summary>
/// <remarks>
/// Safe to leave wired: the requirement is off unless <c>ProxyClientCertificate:Required</c> is true.
/// </remarks>
[PublicAPI]
public static class ProxyClientCertificateRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers <see cref="ProxyClientCertificateOptions"/> from the <c>ProxyClientCertificate</c> configuration
        /// section and the verifier that the pipeline step uses.
        /// </summary>
        /// <remarks>
        /// The section is bound strictly and validated when the host starts: a key that maps to no option, a required
        /// certificate without a readable certificate authority file or without an allowed subject, or a setting
        /// while the requirement is off stops the host with an error naming the entry.
        /// </remarks>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> read for the <c>ProxyClientCertificate</c> section.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddProxyClientCertificate(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(
                ProxyClientCertificateOptions.DefaultSectionName
            );

            // ValidateOnStart materializes the options as the host starts, so a requirement that cannot be honored
            // stops the host before it accepts traffic (ADR-1137).
            serviceCollection
                .AddOptions<ProxyClientCertificateOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<ProxyClientCertificateOptions>,
                    ProxyClientCertificateOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton<ProxyClientCertificateVerifier>();
            serviceCollection.TryAddSingleton(TimeProvider.System);

            return serviceCollection;
        }
    }
}
