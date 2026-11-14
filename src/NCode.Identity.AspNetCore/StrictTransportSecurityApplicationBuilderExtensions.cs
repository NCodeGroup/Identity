// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
        /// request that arrived over HTTPS. It is a <b>no-op</b> when <c>LocalDevelopment:Enabled</c> is set and when
        /// <c>StrictTransportSecurity</c> configuration disables the header, so it is safe to leave wired.
        /// </summary>
        /// <remarks>
        /// Call this right after <c>UseTrustedForwardedHeaders</c> and before HTTPS redirection, so a request that a trusted
        /// proxy received over HTTPS counts as HTTPS. Requires <c>AddStrictTransportSecurity</c>, which also registers
        /// the <c>LocalDevelopment</c> switch.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseStrictTransportSecurity()
        {
            ArgumentNullException.ThrowIfNull(app);

            var services = app.ApplicationServices;

            // Read the switch first: a local inner-loop host never sends the header (ADR-1447).
            if (services.GetRequiredService<LocalDevelopmentMode>().Enabled)
                return app;

            var options = services
                .GetRequiredService<IOptions<StrictTransportSecurityOptions>>()
                .Value;

            return options.Enabled ? app.UseHsts() : app;
        }
    }
}
