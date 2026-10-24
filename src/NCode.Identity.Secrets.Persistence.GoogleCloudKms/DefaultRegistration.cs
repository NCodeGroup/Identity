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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SecretPersistenceLibrary"/> builder to register the Google
/// Cloud Key Management Service root key provider.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The secret-persistence-library builder returned by <c>AddPersistenceLibrary</c>.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IRootKeyProvider"/>, which wraps master keys
        /// with a symmetric encryption key that never leaves the service. The
        /// <c>SecretProtection:RootKeyProviders:GoogleCloudKms</c> section binds
        /// <see cref="GoogleCloudKmsRootKeyOptions"/> strictly. The provider stays inert until the
        /// <c>SecretProtection:RootKeyProvider</c> setting selects <c>google-cloud-kms</c>, and no client is created
        /// before the first key is wrapped or unwrapped. Credentials come from Application Default Credentials, never
        /// from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RootKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddGoogleCloudKmsRootKeyProvider(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<GoogleCloudKmsRootKeyOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(GoogleCloudKmsRootKeyOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<GoogleCloudKmsRootKeyOptions>,
                    GoogleCloudKmsRootKeyOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton<
                IGoogleCloudKmsClientFactory,
                DefaultGoogleCloudKmsClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRootKeyProvider, GoogleCloudKmsRootKeyProvider>()
            );

            return builder;
        }
    }
}
