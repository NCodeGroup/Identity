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
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.StrictTransportSecurity;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Registers the <c>Strict-Transport-Security</c> policy that the server sends on every response to a request that
/// arrived over HTTPS. Apply it to the request pipeline with <c>UseStrictTransportSecurity</c>.
/// </summary>
/// <remarks>
/// Safe to leave wired: the pipeline step adds nothing in the Development environment or when the policy is disabled.
/// </remarks>
[PublicAPI]
public static class StrictTransportSecurityRegistration
{
    private static string[] LoopbackHosts { get; } = ["localhost", "127.0.0.1", "[::1]"];

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers <see cref="StrictTransportSecurityOptions"/> from the <c>StrictTransportSecurity</c> configuration
        /// section and configures the framework's HSTS middleware from it.
        /// </summary>
        /// <remarks>
        /// The section is bound strictly and validated when the host starts: a key that maps to no option, a negative
        /// maximum age, a preload request that the preload list would reject, or an excluded host that is not a host
        /// name stops the host with an error naming the entry, rather than sending a policy the operator did not
        /// configure.
        /// </remarks>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> read for the <c>StrictTransportSecurity</c> section.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddStrictTransportSecurity(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(
                StrictTransportSecurityOptions.DefaultSectionName
            );

            // ValidateOnStart materializes the options as the host starts, so a policy that cannot be honored stops
            // the host before it accepts traffic (ADR-0137).
            serviceCollection
                .AddOptions<StrictTransportSecurityOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<StrictTransportSecurityOptions>,
                    StrictTransportSecurityOptionsValidator
                >()
            );

            serviceCollection
                .AddOptions<HstsOptions>()
                .Configure<IOptions<StrictTransportSecurityOptions>>(
                    (hstsOptions, accessor) => ConfigureHsts(hstsOptions, accessor.Value)
                );

            return serviceCollection;
        }
    }

    private static void ConfigureHsts(
        HstsOptions hstsOptions,
        StrictTransportSecurityOptions options
    )
    {
        hstsOptions.MaxAge = options.MaxAge;
        hstsOptions.IncludeSubDomains = options.IncludeSubDomains;
        hstsOptions.Preload = options.Preload;

        // The framework excludes the loopback hosts by default, and binding appends to a collection's initial items.
        // Replace the defaults with exactly the loopback hosts followed by what was configured.
        hstsOptions.ExcludedHosts.Clear();

        foreach (var host in LoopbackHosts)
        {
            hstsOptions.ExcludedHosts.Add(host);
        }

        foreach (var host in options.ExcludedHosts)
        {
            hstsOptions.ExcludedHosts.Add(host);
        }
    }
}
