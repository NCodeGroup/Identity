// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Registration;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Provides extension methods to configure the PKCE (RFC 7636) <c>code_challenge_method</c> services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the built-in <c>S256</c> code challenge method, its lookup provider, and the
        /// <c>code_challenge_methods_supported</c> setting descriptor, whose domain is the registered methods. A
        /// deployment supports another transformation by registering another <see cref="ICodeChallengeMethod"/> and
        /// listing it in <c>code_challenge_methods_supported</c>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddPkceServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ICodeChallengeMethod, S256CodeChallengeMethod>()
            );
            serviceCollection.TryAddSingleton<
                ICodeChallengeMethodCollectionProvider,
                DefaultCodeChallengeMethodCollectionProvider
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    DefaultPkceSettingDescriptorDataSource
                >()
            );

            return builder;
        }
    }
}
