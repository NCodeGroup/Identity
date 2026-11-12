// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="TrustedForwardedHeadersRegistration"/>: applies the
/// forwarded-headers middleware when a trusted reverse proxy is configured.
/// </summary>
// See ADR-1447.
[PublicAPI]
public static class TrustedForwardedHeadersApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the forwarded-headers middleware when <c>ForwardedHeaders</c> configuration names at least one trusted
        /// proxy or network, so the application observes the original scheme and host rather than the
        /// proxy-to-application hop. It is a <b>no-op</b> when no trusted proxy is configured, so it is safe to leave
        /// wired for a direct-TLS deployment.
        /// </summary>
        /// <remarks>
        /// Call this right after <c>UseProxyClientCertificate</c> where that is used, because that middleware must be
        /// the first one, and otherwise <b>first</b> in the pipeline — ahead of HTTPS redirection, static files,
        /// routing, and the authentication middleware. Everything downstream that reads the scheme or host (the HTTPS
        /// redirect, Strict-Transport-Security, and every URL the application builds from the request) must observe the
        /// corrected values rather than the proxy hop. Requires <c>AddTrustedForwardedHeaders</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseTrustedForwardedHeaders()
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app
                .ApplicationServices.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>()
                .Value;

            return options.HasTrustedProxy ? app.UseForwardedHeaders() : app;
        }
    }
}
