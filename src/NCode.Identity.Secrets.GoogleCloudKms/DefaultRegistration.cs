// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SecretPersistenceLibrary"/> builder to register the Google
/// Cloud Key Management Service key wrapper and remote key provider.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The secret-persistence-library builder returned by <c>AddPersistenceLibrary</c>.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IKeyWrapper"/>, which a deployment selects as its root key wrapper with <c>SecretProtection:RootKeyProvider</c> = <c>google-cloud-kms</c>, and which wraps master keys
        /// with a symmetric encryption key that never leaves the service. The
        /// <c>SecretProtection:RootKeyProviders:GoogleCloudKms</c> section binds
        /// <see cref="GoogleCloudKmsKeyWrapperOptions"/> strictly. The provider stays inert until the
        /// <c>SecretProtection:RootKeyProvider</c> setting selects <c>google-cloud-kms</c>, and no client is created
        /// before the first key is wrapped or unwrapped. Credentials come from Application Default Credentials, never
        /// from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RootKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddGoogleCloudKmsKeyWrapper(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<GoogleCloudKmsKeyWrapperOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(GoogleCloudKmsKeyWrapperOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<GoogleCloudKmsKeyWrapperOptions>,
                    GoogleCloudKmsKeyWrapperOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IGoogleCloudKmsClientFactory,
                DefaultGoogleCloudKmsClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IKeyWrapper, GoogleCloudKmsKeyWrapper>()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IRemoteKeyProvider"/>, which creates tenant
        /// signing keys that never leave the service, signs with them, and destroys them, together with the
        /// <see cref="IRemoteSigningAlgorithmSource"/> that maps each JOSE signature algorithm to the crypto key version
        /// algorithm of the service. The <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section binds
        /// <see cref="GoogleCloudKmsRemoteKeyProviderOptions"/> strictly and is validated when the host starts, so
        /// registering the provider selects it. No client is created before the first key is created, signed with or
        /// destroyed. Credentials come from Application Default Credentials, never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddGoogleCloudKmsRemoteKeyProvider(
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
                    IRemoteSigningAlgorithmSource,
                    GoogleCloudKmsSigningAlgorithms
                >()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Google Cloud Key Management Service <see cref="IRemoteKeyProvider"/> and its
        /// <see cref="IRemoteSigningAlgorithmSource"/> as <c>AddGoogleCloudKmsRemoteKeyProvider</c> does, but
        /// only when the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section exists. Registering a
        /// remote key provider selects it and validates its options when the host starts, so this method registers it
        /// only when its configuration section exists.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> that may hold the <c>SecretProtection:RemoteKeyProviders:GoogleCloudKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddGoogleCloudKmsRemoteKeyProviderIfConfigured(
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
