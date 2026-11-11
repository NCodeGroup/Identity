// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="StrictTransportSecurityRegistration"/>: applies the
/// HSTS middleware that sends the <c>Strict-Transport-Security</c> header.
/// </summary>
[PublicAPI]
public static class StrictTransportSecurityApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the HSTS middleware, which sends the <c>Strict-Transport-Security</c> header on every response to a
        /// request that arrived over HTTPS. It is a <b>no-op</b> in the Development environment and when
        /// <c>StrictTransportSecurity</c> configuration disables the header, so it is safe to leave wired.
        /// </summary>
        /// <remarks>
        /// Call this right after <c>UseTrustedForwardedHeaders</c> and before HTTPS redirection, so a request that a trusted
        /// proxy received over HTTPS counts as HTTPS. Requires <c>AddStrictTransportSecurity</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseStrictTransportSecurity()
        {
            ArgumentNullException.ThrowIfNull(app);

            var services = app.ApplicationServices;

            // Read the environment first: Development never sends the header (ADR-1258).
            if (services.GetRequiredService<IHostEnvironment>().IsDevelopment())
                return app;

            var options = services
                .GetRequiredService<IOptions<StrictTransportSecurityOptions>>()
                .Value;

            return options.Enabled ? app.UseHsts() : app;
        }
    }
}
