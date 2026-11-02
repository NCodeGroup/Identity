// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
/// Allowed origins come from <see cref="DeveloperCorsOptions"/>, bound from the <c>Cors</c> configuration section. The
/// section is the opt-in: without it no policy is registered, and the library carries no origins of its own. The host's
/// Development overlay lists the origins it allows (ADR-1313). This is intentionally NOT part of the default
/// registrations.
/// </remarks>
[PublicAPI]
public static class DeveloperCorsRegistration
{
    /// <summary>
    /// The name of the CORS policy registered by <c>AddDeveloperCors</c> and applied by <c>UseDeveloperCors</c>.
    /// </summary>
    public const string PolicyName = "NCode.Identity.Server.DeveloperCors";

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the development CORS policy (<see cref="PolicyName"/>) whose allowed origins come from
        /// <see cref="DeveloperCorsOptions"/>. It registers nothing without a <c>Cors</c> section: the library carries no
        /// origins, and the host's Development overlay lists them (ADR-1313). Apply the policy to the request pipeline
        /// with <c>UseDeveloperCors</c>. It must not be used to grant production cross-origin access implicitly.
        /// </summary>
        /// <remarks>
        /// The <c>Cors</c> section is bound strictly and validated when the host starts: a key that maps to no option,
        /// or an allowed origin that is not written as a browser sends it (a trailing slash, a path, a wildcard), stops
        /// the host with an error naming the entry, because such an origin can never match a request.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the <c>Cors</c> section.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDeveloperCors(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(DeveloperCorsOptions.DefaultSectionName);
            if (!section.Exists())
            {
                return serviceCollection;
            }

            // ValidateOnStart materializes the options as the host starts, so an origin that can never match stops the
            // host before it accepts traffic (ADR-1137).
            serviceCollection
                .AddOptions<DeveloperCorsOptions>()
                .Configure(options => section.BindStrictly(options))
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
