// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Registers HTTP logging that writes the request and response properties, the allow-listed headers and the duration,
/// and a body only for an endpoint that opts in. Apply it with the framework's <c>UseHttpLogging</c>, right after
/// <c>UseRouting</c>.
/// </summary>
[PublicAPI]
public static class GuardedHttpLoggingRegistration
{
    private static void CapHostingDiagnostics(LoggerFilterOptions options)
    {
        var providerNames = options.Rules.Select(rule => rule.ProviderName).Distinct().ToList();
        providerNames.Add(null);

        foreach (var providerName in providerNames.Distinct())
        {
            var rule = new LoggerFilterRule(
                providerName,
                GuardedHttpLoggingDefaults.HostingDiagnosticsCategory,
                LogLevel.Warning,
                filter: null
            );
            options.Rules.Add(rule);
        }
    }

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the framework's HTTP logging with the permitted fields, without <c>Location</c> in the response
        /// header allow-list, together with the interceptor that adds the bodies an endpoint opts into.
        /// </summary>
        /// <remarks>
        /// Options that enable a body, the query string or a trailer, or allow-list a credential header, stop the host
        /// when it starts. It also caps the hosting layer's request logging
        /// (<c>Microsoft.AspNetCore.Hosting.Diagnostics</c>) at warnings, because its request lines carry the query
        /// string, so a raised log level writes the request only through HTTP logging.
        /// </remarks>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddGuardedHttpLogging()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.AddHttpLogging(options =>
            {
                options.LoggingFields = GuardedHttpLoggingDefaults.PermittedFields;
                options.ResponseHeaders.Remove(HeaderNames.Location);
            });
            serviceCollection.AddHttpLoggingInterceptor<GuardedHttpLoggingInterceptor>();
            var validatorDescriptor = ServiceDescriptor.Singleton<
                IValidateOptions<HttpLoggingOptions>,
                GuardedHttpLoggingOptionsValidator
            >();
            serviceCollection.TryAddEnumerable(validatorDescriptor);
            serviceCollection.AddOptions<HttpLoggingOptions>().ValidateOnStart();

            // The hosting layer's request lines carry the query string, and HTTP logging's records take their place.
            // A rule scoped to a provider outranks one that is not, so the cap is added for each provider that has a
            // rule, and as a post-configuration it follows every configuration section and every filter.
            var loggerFilterBuilder = serviceCollection.AddOptions<LoggerFilterOptions>();
            loggerFilterBuilder.PostConfigure(CapHostingDiagnostics);

            return serviceCollection;
        }
    }
}
