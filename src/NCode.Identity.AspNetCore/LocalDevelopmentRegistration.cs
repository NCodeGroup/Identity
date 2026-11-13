// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.Registration;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Registers the explicit switch that turns on the relaxations a local inner-loop host needs.
/// </summary>
/// <remarks>
/// Safe to leave wired: the switch is off unless <c>LocalDevelopment:Enabled</c> is true, and the Production
/// environment refuses it.
/// </remarks>
[PublicAPI]
public static class LocalDevelopmentRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers <see cref="LocalDevelopmentOptions"/> from the <c>LocalDevelopment</c> configuration section.
        /// Calling it more than once registers the options once.
        /// </summary>
        /// <remarks>
        /// The section is bound strictly and validated when the host starts: a key that maps to no option, or the
        /// switch turned on in the Production environment, stops the host with an error naming the entry.
        /// </remarks>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> read for the <c>LocalDevelopment</c> section.
        /// </param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddLocalDevelopmentOptions(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            if (
                serviceCollection.Any(descriptor =>
                    descriptor.ServiceType == typeof(IValidateOptions<LocalDevelopmentOptions>)
                    && descriptor.ImplementationType == typeof(LocalDevelopmentOptionsValidator)
                )
            )
            {
                return serviceCollection;
            }

            var section = configuration.GetSection(LocalDevelopmentOptions.DefaultSectionName);

            // ValidateOnStart materializes the options as the host starts, so a switch that the environment refuses
            // stops the host before it accepts traffic (ADR-1137).
            serviceCollection
                .AddOptions<LocalDevelopmentOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<LocalDevelopmentOptions>,
                    LocalDevelopmentOptionsValidator
                >()
            );

            return serviceCollection;
        }
    }
}
