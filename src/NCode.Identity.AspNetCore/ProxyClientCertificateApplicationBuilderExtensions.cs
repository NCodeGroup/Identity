// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.AspNetCore.ProxyClientCertificate;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="ProxyClientCertificateRegistration"/>: refuses a request
/// whose connection does not carry the reverse proxy's client certificate.
/// </summary>
[PublicAPI]
public static class ProxyClientCertificateApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the middleware that answers <c>403</c> to every request, except the liveness and readiness probes,
        /// whose connection carries no trusted certificate from the reverse proxy. Adds nothing unless
        /// <c>ProxyClientCertificate:Required</c> is true.
        /// </summary>
        /// <remarks>
        /// Must be the first middleware in the pipeline, so that no other middleware sees a request that the proxy did
        /// not make. Requires <see cref="ProxyClientCertificateRegistration.AddProxyClientCertificate"/>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseProxyClientCertificate()
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app
                .ApplicationServices.GetRequiredService<IOptions<ProxyClientCertificateOptions>>()
                .Value;

            return options.Required ? app.UseMiddleware<ProxyClientCertificateMiddleware>() : app;
        }
    }
}
