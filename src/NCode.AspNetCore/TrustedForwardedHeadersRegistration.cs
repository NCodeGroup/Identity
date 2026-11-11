// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.AspNetCore.Options;
using NCode.Identity.Registration;

namespace NCode.AspNetCore;

/// <summary>
/// Registers trust in a reverse proxy's <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
/// headers, so the application observes the original scheme and host when TLS terminates ahead of it. Apply it to the
/// request pipeline with <c>UseTrustedForwardedHeaders</c>.
/// </summary>
/// <remarks>
/// Safe to leave wired: with no trusted proxy configured the middleware is never added, and the application uses the
/// connection it actually received.
/// </remarks>
// See ADR-1447.
[PublicAPI]
public static class TrustedForwardedHeadersRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers <see cref="TrustedForwardedHeadersOptions"/> from the <c>ForwardedHeaders</c> configuration
        /// section and configures the forwarded-headers middleware to honor <c>X-Forwarded-For</c>,
        /// <c>X-Forwarded-Proto</c>, and <c>X-Forwarded-Host</c> from the configured proxies only.
        /// </summary>
        /// <remarks>
        /// The section is bound strictly and validated when the host starts: a key that maps to no option, a trusted
        /// proxy that is not an IP address, a trusted network that is not in CIDR notation, or a forward limit below
        /// one stops the host with an error naming the entry, rather than leaving the application trusting less than
        /// the operator configured.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the <c>ForwardedHeaders</c> section.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddTrustedForwardedHeaders(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(
                TrustedForwardedHeadersOptions.DefaultSectionName
            );

            // ValidateOnStart materializes the options as the host starts, so an entry that cannot be honored stops
            // the host before it accepts traffic (ADR-1137).
            serviceCollection
                .AddOptions<TrustedForwardedHeadersOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<TrustedForwardedHeadersOptions>,
                    TrustedForwardedHeadersOptionsValidator
                >()
            );

            serviceCollection
                .AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<TrustedForwardedHeadersOptions>>(
                    (forwardedHeadersOptions, trustedForwardedHeadersOptionsAccessor) =>
                        ConfigureForwardedHeaders(
                            forwardedHeadersOptions,
                            trustedForwardedHeadersOptionsAccessor.Value
                        )
                );

            return serviceCollection;
        }
    }

    private static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions forwardedHeadersOptions,
        TrustedForwardedHeadersOptions trustedForwardedHeadersOptions
    )
    {
        forwardedHeadersOptions.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost;

        forwardedHeadersOptions.ForwardLimit = trustedForwardedHeadersOptions.ForwardLimit;

        // The framework defaults trust loopback, which is wrong in both directions here: a proxy in another container
        // is not loopback (so forwarding would silently do nothing), and anything else that reaches the application
        // directly must not be trusted. Replace the defaults with exactly what was configured; the validator has
        // already rejected every entry that does not parse.
        forwardedHeadersOptions.KnownProxies.Clear();
        forwardedHeadersOptions.KnownIPNetworks.Clear();

        foreach (var proxy in trustedForwardedHeadersOptions.KnownProxies)
        {
            var address = IPAddress.Parse(proxy);
            forwardedHeadersOptions.KnownProxies.Add(address);
        }

        foreach (var network in trustedForwardedHeadersOptions.KnownNetworks)
        {
            // System.Net.IPNetwork, not the obsolete Microsoft.AspNetCore.HttpOverrides type of the same name.
            var ipNetwork = System.Net.IPNetwork.Parse(network);
            forwardedHeadersOptions.KnownIPNetworks.Add(ipNetwork);
        }
    }
}
