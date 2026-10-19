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

using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Server.Configuration;
using NCode.Identity.Server.ReverseProxy;

namespace NCode.Identity.Server;

/// <summary>
/// Registers trust in a reverse proxy's <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
/// headers, so the server observes the original scheme and host when TLS terminates ahead of it. Apply it to the
/// request pipeline with <c>UseReverseProxy</c>.
/// </summary>
/// <remarks>
/// Safe to leave wired: with no trusted proxy configured the middleware is never added, and the server uses the
/// connection it actually received.
/// </remarks>
[PublicAPI]
public static class ReverseProxyRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers <see cref="ReverseProxyOptions"/> from the <c>ReverseProxy</c> configuration section and
        /// configures the forwarded-headers middleware to honor <c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c>, and
        /// <c>X-Forwarded-Host</c> from the configured proxies only. The section is bound strictly: a key that maps to
        /// no option throws.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the <c>ReverseProxy</c> section.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">The <c>ReverseProxy</c> section contains a key that maps to no
        /// option.</exception>
        public IServiceCollection AddReverseProxy(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            var options = new ReverseProxyOptions();
            var section = configuration.GetSection(ReverseProxyOptions.DefaultSectionName);
            section.BindStrictly(options);

            serviceCollection.AddSingleton(options);

            serviceCollection.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
            {
                forwardedHeadersOptions.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor
                    | ForwardedHeaders.XForwardedProto
                    | ForwardedHeaders.XForwardedHost;

                forwardedHeadersOptions.ForwardLimit = options.ForwardLimit;

                // The framework defaults trust loopback, which is wrong in both directions here: a proxy in another
                // container is not loopback (so forwarding would silently do nothing), and anything else that reaches
                // the application directly must not be trusted. Replace the defaults with exactly what was configured.
                forwardedHeadersOptions.KnownProxies.Clear();
                forwardedHeadersOptions.KnownIPNetworks.Clear();

                foreach (var candidate in options.KnownProxies)
                {
                    if (IPAddress.TryParse(candidate, out var address))
                    {
                        forwardedHeadersOptions.KnownProxies.Add(address);
                    }
                }

                foreach (var candidate in options.KnownNetworks)
                {
                    // System.Net.IPNetwork, not the obsolete Microsoft.AspNetCore.HttpOverrides type of the same name
                    // that KnownNetworks used to take.
                    if (System.Net.IPNetwork.TryParse(candidate, out var network))
                    {
                        forwardedHeadersOptions.KnownIPNetworks.Add(network);
                    }
                }
            });

            return serviceCollection;
        }
    }
}
