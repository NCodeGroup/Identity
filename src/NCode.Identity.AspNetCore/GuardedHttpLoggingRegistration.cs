// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the framework's HTTP logging with the permitted fields, without <c>Location</c> in the response
        /// header allow-list, together with the interceptor that adds the bodies an endpoint opts into.
        /// </summary>
        /// <remarks>
        /// Options that enable a body, the query string or a trailer, or allow-list a credential header, stop the host
        /// when it starts.
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
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<HttpLoggingOptions>,
                    GuardedHttpLoggingOptionsValidator
                >()
            );
            serviceCollection.AddOptions<HttpLoggingOptions>().ValidateOnStart();

            return serviceCollection;
        }
    }
}
