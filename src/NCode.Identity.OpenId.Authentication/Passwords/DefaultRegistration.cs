// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Settings;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Provides extension methods to configure the password policy services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the password policy, its built-in blocklists, the password attempt limiter, and the password
        /// setting descriptors. A deployment adds a blocklist by registering another <see cref="IPasswordBlocklist"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddPasswordServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<IPasswordPolicy, DefaultPasswordPolicy>();
            serviceCollection.TryAddSingleton<
                IPasswordAttemptLimiter,
                DefaultPasswordAttemptLimiter
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPasswordBlocklist, CommonPasswordBlocklist>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPasswordBlocklist, AccountNamePasswordBlocklist>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    DefaultPasswordSettingDescriptorDataSource
                >()
            );

            return builder;
        }
    }
}
