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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.StrictTransportSecurity;

namespace NCode.Identity.Server;

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
        /// Call this right after <c>UseReverseProxy</c> and before HTTPS redirection, so a request that a trusted
        /// proxy received over HTTPS counts as HTTPS. Requires <c>AddStrictTransportSecurity</c>.
        /// </remarks>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseStrictTransportSecurity()
        {
            ArgumentNullException.ThrowIfNull(app);

            var services = app.ApplicationServices;

            // Read the environment first: Development never sends the header (ADR-0258).
            if (services.GetRequiredService<IHostEnvironment>().IsDevelopment())
                return app;

            var options = services
                .GetRequiredService<IOptions<StrictTransportSecurityOptions>>()
                .Value;

            return options.Enabled ? app.UseHsts() : app;
        }
    }
}
