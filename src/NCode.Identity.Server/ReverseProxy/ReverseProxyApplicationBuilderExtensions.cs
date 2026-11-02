// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.ReverseProxy;

namespace NCode.Identity.Server;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="ReverseProxyRegistration"/>: applies the forwarded-headers
/// middleware when a trusted reverse proxy is configured.
/// </summary>
[PublicAPI]
public static class ReverseProxyApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the forwarded-headers middleware when <c>ReverseProxy</c> configuration names at least one trusted
        /// proxy or network, so the server observes the original scheme and host rather than the proxy-to-application
        /// hop. It is a <b>no-op</b> when no trusted proxy is configured, so it is safe to leave wired for a
        /// direct-TLS deployment.
        /// </summary>
        /// <remarks>
        /// Call this <b>first</b> in the pipeline — ahead of HTTPS redirection, static files, routing, and the
        /// authentication middleware. Everything downstream that reads the scheme or host (the HTTPS redirect, the
        /// tenant's base address, and therefore the issuer and the published endpoint URLs) must observe the corrected
        /// values rather than the proxy hop. Requires <c>AddReverseProxy</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseReverseProxy()
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app
                .ApplicationServices.GetRequiredService<IOptions<ReverseProxyOptions>>()
                .Value;

            return options.HasTrustedProxy ? app.UseForwardedHeaders() : app;
        }
    }
}
