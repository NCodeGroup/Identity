// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="TransportSecurityRegistration"/>: applies the HTTPS
/// redirect only in the environments that can bind a plain-HTTP endpoint.
/// </summary>
// See ADR-1503.
[PublicAPI]
public static class TransportSecurityApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the framework's HTTPS redirection middleware in every environment except Production. Production serves
        /// HTTPS only, so it has no redirect to make and the middleware is not added.
        /// </summary>
        /// <remarks>
        /// Every other environment keeps the framework's redirect, which a host that binds HTTPS and HTTP together
        /// relies on. Requires the defaults of <c>AddHttpsRedirection</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseHttpsRedirectionOutsideProduction()
        {
            ArgumentNullException.ThrowIfNull(app);

            var environment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

            return environment.IsProduction() ? app : app.UseHttpsRedirection();
        }
    }
}
