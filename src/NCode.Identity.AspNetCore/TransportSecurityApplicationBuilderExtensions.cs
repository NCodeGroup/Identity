// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="TransportSecurityRegistration"/>: applies the HTTPS
/// redirect only to a local inner-loop host that can bind a plain-HTTP endpoint.
/// </summary>
// See ADR-1504.
[PublicAPI]
public static class TransportSecurityApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the framework's HTTPS redirection middleware only when <c>LocalDevelopment:Enabled</c> is set. A
        /// deployment serves HTTPS only and refuses a plain-HTTP endpoint at the transport check, so it has no redirect
        /// to make and the middleware is not added.
        /// </summary>
        /// <remarks>
        /// A local inner-loop host keeps the framework's redirect, which a host that binds HTTPS and HTTP together
        /// relies on. Requires <c>AddTransportSecurity</c> or <c>AddLocalDevelopmentOptions</c>, and the defaults of
        /// <c>AddHttpsRedirection</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseLocalDevelopmentHttpsRedirection()
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app
                .ApplicationServices.GetRequiredService<IOptions<LocalDevelopmentOptions>>()
                .Value;

            return options.Enabled ? app.UseHttpsRedirection() : app;
        }
    }
}
