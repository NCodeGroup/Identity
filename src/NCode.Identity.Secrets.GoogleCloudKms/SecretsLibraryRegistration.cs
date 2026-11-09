// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Provides opt-in extension methods that register the Google Cloud Key Management Service remote key provider on the
/// secrets-library builder, beside the key wrapper <c>DefaultRegistration</c> registers on the
/// secret-persistence-library builder.
/// </summary>
// See ADR-1069.
[PublicAPI]
public static class SecretsLibraryRegistration
{
    /// <param name="builder">The secrets-library builder returned by <c>AddSecretsLibrary</c>.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IRemoteKeyProvider"/>, which holds the keys
        /// of tenants and of client key rings, creates tenant keys that never leave the service, signs, decrypts and
        /// agrees with them, and destroys them, together with the
        /// <see cref="IRemoteKeyDescriptorSource"/> that describes the provider and maps each JOSE algorithm to
        /// the crypto key version algorithm of the service. The <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section binds
        /// <see cref="GoogleCloudKmsRemoteKeyProviderOptions"/> strictly and is validated when the host starts.
        /// Without a key ring the provider creates and lists no keys and still reads, signs with, decrypts with and
        /// destroys keys by their names. No client is created before the first key is created, signed with,
        /// decrypted with or destroyed. Credentials come from Application Default Credentials, never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretsLibrary> AddGoogleCloudKmsRemoteKeyProvider(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<GoogleCloudKmsRemoteKeyProviderOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(GoogleCloudKmsRemoteKeyProviderOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<GoogleCloudKmsRemoteKeyProviderOptions>,
                    GoogleCloudKmsRemoteKeyProviderOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IGoogleCloudKmsClientFactory,
                DefaultGoogleCloudKmsClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRemoteKeyProvider, GoogleCloudKmsRemoteKeyProvider>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IRemoteKeyDescriptorSource,
                    GoogleCloudKmsRemoteKeyDescriptors
                >()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IRemoteKeyProvider"/> and its
        /// <see cref="IRemoteKeyDescriptorSource"/> as <c>AddGoogleCloudKmsRemoteKeyProvider</c> does, but
        /// only when the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section exists. A section
        /// without a key ring registers a provider that creates no keys.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> that may hold the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretsLibrary> AddGoogleCloudKmsRemoteKeyProviderIfConfigured(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            return configuration
                .GetSection(GoogleCloudKmsRemoteKeyProviderOptions.DefaultSectionName)
                .Exists()
                ? builder.AddGoogleCloudKmsRemoteKeyProvider(configuration)
                : builder;
        }
    }
}
