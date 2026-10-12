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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, development-oriented opt-in that enables browser cross-origin (CORS) access to the OpenID
/// server from a local single-page-application dev server (for example, the Vite dev server), so the SPA can read the
/// discovery document and call the management API during local development.
/// </summary>
/// <remarks>
/// Allowed origins come from the <c>Cors:AllowedOrigins</c> configuration array. When that is absent, a Development
/// host defaults to the local Vite dev/preview origins; outside Development (and without explicit configuration) the
/// registered policy allows no origins. This is intentionally NOT part of the default registrations.
/// </remarks>
[PublicAPI]
public static class DeveloperCorsRegistration
{
    /// <summary>
    /// The name of the CORS policy registered by <c>AddDeveloperCors</c> and applied by <c>UseDeveloperCors</c>.
    /// </summary>
    public const string PolicyName = "NCode.Identity.Server.DeveloperCors";

    private const string AllowedOriginsConfigurationKey = "Cors:AllowedOrigins";

    private static readonly string[] DefaultDevelopmentOrigins =
    [
        // Management console dev server (apps/mgmt).
        "http://localhost:5173",
        "https://localhost:5173",
        // Playground flow-explorer dev server (apps/playground).
        "http://localhost:5174",
        "https://localhost:5174",
        // Conformance posture dashboard dev server (apps/conformance).
        "http://localhost:5175",
        "https://localhost:5175",
        // Vite preview origin.
        "http://localhost:4173",
        "https://localhost:4173",
    ];

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the development CORS policy (<see cref="PolicyName"/>) whose allowed origins come from the
        /// <c>Cors:AllowedOrigins</c> configuration array, defaulting to the local Vite dev/preview origins in a
        /// Development environment. Apply it to the request pipeline with <c>UseDeveloperCors</c>. It is a no-op when
        /// no origins resolve (so it is safe to leave wired), and must not be used to grant production cross-origin
        /// access implicitly — configure <c>Cors:AllowedOrigins</c> explicitly instead.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for <c>Cors:AllowedOrigins</c>.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> used to apply the Development default.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDeveloperCors(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment
        )
        {
            var origins = configuration
                .GetSection(AllowedOriginsConfigurationKey)
                .GetChildren()
                .Select(child => child.Value)
                .OfType<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (origins.Length == 0 && hostEnvironment.IsDevelopment())
            {
                origins = DefaultDevelopmentOrigins;
            }

            serviceCollection.AddCors(options =>
                options.AddPolicy(
                    PolicyName,
                    policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
                )
            );

            return serviceCollection;
        }
    }
}
