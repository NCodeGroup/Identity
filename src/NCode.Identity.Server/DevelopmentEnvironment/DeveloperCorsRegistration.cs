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
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides an explicit, development-oriented opt-in that enables browser cross-origin (CORS) access to the OpenID
/// server from a local single-page-application dev server (for example, the Vite dev server), so the SPA can read the
/// discovery document and call the management API during local development.
/// </summary>
/// <remarks>
/// Allowed origins come from <see cref="DeveloperCorsOptions"/>, bound from the <c>Cors</c> configuration section. When
/// none are configured, a Development host defaults to the local Vite dev/preview origins; any other environment allows
/// no origins. This is intentionally NOT part of the default registrations, and <c>AddDevelopmentEnvironment</c> does
/// not register it in Production.
/// </remarks>
[PublicAPI]
public static class DeveloperCorsRegistration
{
    /// <summary>
    /// The name of the CORS policy registered by <c>AddDeveloperCors</c> and applied by <c>UseDeveloperCors</c>.
    /// </summary>
    public const string PolicyName = "NCode.Identity.Server.DeveloperCors";

    private static readonly string[] DefaultDevelopmentOrigins =
    [
        // Management console dev server (apps/mgmt).
        "https://localhost:5173",
        // Playground flow-explorer dev server (apps/playground).
        "https://localhost:5174",
        // Conformance posture dashboard dev server (apps/conformance).
        "https://localhost:5175",
        // Vite preview origin.
        "http://localhost:4173",
        "https://localhost:4173",
    ];

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the development CORS policy (<see cref="PolicyName"/>) whose allowed origins come from
        /// <see cref="DeveloperCorsOptions"/>, defaulting to the local Vite dev/preview origins in a Development
        /// environment. Apply it to the request pipeline with <c>UseDeveloperCors</c>. It must not be used to grant
        /// production cross-origin access implicitly.
        /// </summary>
        /// <remarks>
        /// The <c>Cors</c> section is bound strictly and validated when the host starts: a key that maps to no option,
        /// or an allowed origin that is not written as a browser sends it (a trailing slash, a path, a wildcard), stops
        /// the host with an error naming the entry, because such an origin can never match a request.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the <c>Cors</c> section.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> used to apply the Development default.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDeveloperCors(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            var section = configuration.GetSection(DeveloperCorsOptions.DefaultSectionName);
            var isDevelopment = hostEnvironment.IsDevelopment();

            // ValidateOnStart materializes the options as the host starts, so an origin that can never match stops the
            // host before it accepts traffic (ADR-0137).
            serviceCollection
                .AddOptions<DeveloperCorsOptions>()
                .Configure(options => section.BindStrictly(options))
                .PostConfigure(options =>
                    options.AllowedOrigins =
                        options.AllowedOrigins.Length == 0 && isDevelopment
                            ? [.. DefaultDevelopmentOrigins]
                            : options.AllowedOrigins
                )
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<DeveloperCorsOptions>,
                    DeveloperCorsOptionsValidator
                >()
            );

            serviceCollection.AddCors();
            serviceCollection
                .AddOptions<CorsOptions>()
                .Configure<IOptions<DeveloperCorsOptions>>(
                    (corsOptions, developerCorsOptionsAccessor) =>
                        corsOptions.AddPolicy(
                            PolicyName,
                            policy =>
                                policy
                                    .WithOrigins(developerCorsOptionsAccessor.Value.AllowedOrigins)
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()
                        )
                );

            return serviceCollection;
        }
    }
}
