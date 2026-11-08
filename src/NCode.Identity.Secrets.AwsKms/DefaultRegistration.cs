// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.AwsKms.Facades;
using NCode.Identity.Secrets.AwsKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets.AwsKms;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SecretPersistenceLibrary"/> builder to register the AWS Key
/// Management Service key wrapper and remote key provider.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The secret-persistence-library builder returned by <c>AddPersistenceLibrary</c>.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the AWS Key Management Service <see cref="IKeyWrapper"/>, which a deployment selects as its root key wrapper with <c>SecretProtection:RootKeyProvider</c> = <c>aws-kms</c>, and which wraps master keys with a
        /// symmetric encryption key that never leaves the service. The <c>SecretProtection:RootKeyProviders:AwsKms</c>
        /// section binds <see cref="AwsKmsKeyWrapperOptions"/> strictly. The provider stays inert until the
        /// <c>SecretProtection:RootKeyProvider</c> setting selects <c>aws-kms</c>, and no client is created before
        /// the first key is wrapped or unwrapped. Credentials come from the default credential chain of the AWS SDK,
        /// never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RootKeyProviders:AwsKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAwsKmsKeyWrapper(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<AwsKmsKeyWrapperOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(AwsKmsKeyWrapperOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AwsKmsKeyWrapperOptions>,
                    AwsKmsKeyWrapperOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<IAwsKmsClientFactory, DefaultAwsKmsClientFactory>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IKeyWrapper, AwsKmsKeyWrapper>()
            );

            return builder;
        }

        /// <summary>
        /// Registers the AWS Key Management Service <see cref="IRemoteKeyProvider"/>, which creates tenant signing
        /// keys that never leave the service, signs with them, and schedules them for deletion, together with the
        /// <see cref="IRemoteSigningAlgorithmSource"/> that maps each JOSE signature algorithm to the key
        /// specification and signing algorithm of the service. The
        /// <c>SecretProtection:RemoteKeyProviders:AwsKms</c> section binds
        /// <see cref="AwsKmsRemoteKeyProviderOptions"/> strictly and is validated when the host starts, so
        /// registering the provider selects it. No client is created before the first key is created, signed with or
        /// destroyed. Credentials come from the default credential chain of the AWS SDK, never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RemoteKeyProviders:AwsKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAwsKmsRemoteKeyProvider(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<AwsKmsRemoteKeyProviderOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(AwsKmsRemoteKeyProviderOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AwsKmsRemoteKeyProviderOptions>,
                    AwsKmsRemoteKeyProviderOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<IAwsKmsClientFactory, DefaultAwsKmsClientFactory>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRemoteKeyProvider, AwsKmsRemoteKeyProvider>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IRemoteSigningAlgorithmSource,
                    AwsKmsSigningAlgorithms
                >()
            );

            return builder;
        }

        /// <summary>
        /// Registers the AWS Key Management Service <see cref="IRemoteKeyProvider"/> and its
        /// <see cref="IRemoteSigningAlgorithmSource"/> as <c>AddAwsKmsRemoteKeyProvider</c> does, but only when
        /// the <c>SecretProtection:RemoteKeyProviders:AwsKms</c> section exists. Registering a remote key provider
        /// selects it and validates its options when the host starts, so this method registers it only when its
        /// configuration section exists.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> that may hold the <c>SecretProtection:RemoteKeyProviders:AwsKms</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAwsKmsRemoteKeyProviderIfConfigured(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            return configuration
                .GetSection(AwsKmsRemoteKeyProviderOptions.DefaultSectionName)
                .Exists()
                ? builder.AddAwsKmsRemoteKeyProvider(configuration)
                : builder;
        }
    }
}
