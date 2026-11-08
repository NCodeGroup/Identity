// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets.AzureKeyVault;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SecretPersistenceLibrary"/> builder to register the Azure Key
/// Vault key wrapper and remote key provider.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The secret-persistence-library builder returned by <c>AddPersistenceLibrary</c>.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the Azure Key Vault <see cref="IKeyWrapper"/>, which a deployment selects as its root key wrapper with <c>SecretProtection:RootKeyProvider</c> = <c>azure-key-vault</c>, and which wraps master keys with an RSA key in
        /// Azure Key Vault or Managed HSM that never leaves the service. The
        /// <c>SecretProtection:RootKeyProviders:AzureKeyVault</c> section binds
        /// <see cref="AzureKeyVaultKeyWrapperOptions"/> strictly. The provider stays inert until the
        /// <c>SecretProtection:RootKeyProvider</c> setting selects <c>azure-key-vault</c>, and no client is created
        /// before the first key is wrapped or unwrapped. Credentials come from
        /// <see cref="Azure.Identity.DefaultAzureCredential"/>, never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RootKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAzureKeyVaultKeyWrapper(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<AzureKeyVaultKeyWrapperOptions>()
                .Configure(options =>
                {
                    var section = configuration.GetSection(
                        AzureKeyVaultKeyWrapperOptions.DefaultSectionName
                    );

                    // Binding an array adds the configured items to the ones it already holds, so a configured list
                    // starts from an empty one and replaces the default suffixes.
                    if (
                        section
                            .GetSection(
                                nameof(AzureKeyVaultKeyWrapperOptions.ReferencedKeyHostSuffixes)
                            )
                            .Exists()
                    )
                    {
                        options.ReferencedKeyHostSuffixes = [];
                    }

                    section.BindStrictly(options);
                })
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AzureKeyVaultKeyWrapperOptions>,
                    AzureKeyVaultKeyWrapperOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IAzureKeyVaultClientFactory,
                DefaultAzureKeyVaultClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IKeyWrapper, AzureKeyVaultKeyWrapper>()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Azure Key Vault <see cref="IRemoteKeyProvider"/>, which creates tenant signing keys in Azure
        /// Key Vault or Managed HSM that never leave the service, signs with them, and disables them, together with
        /// the <see cref="IRemoteSigningAlgorithmSource"/> that maps each JOSE signature algorithm to the key type and
        /// signature algorithm of the service. The <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section
        /// binds <see cref="AzureKeyVaultRemoteKeyProviderOptions"/> strictly and is validated when the host starts,
        /// so registering the provider selects it. No client is created before the first key is created, signed with
        /// or destroyed. Credentials come from <see cref="Azure.Identity.DefaultAzureCredential"/>, never from
        /// configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAzureKeyVaultRemoteKeyProvider(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<AzureKeyVaultRemoteKeyProviderOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(AzureKeyVaultRemoteKeyProviderOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AzureKeyVaultRemoteKeyProviderOptions>,
                    AzureKeyVaultRemoteKeyProviderOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<
                IAzureKeyVaultClientFactory,
                DefaultAzureKeyVaultClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRemoteKeyProvider, AzureKeyVaultRemoteKeyProvider>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IRemoteSigningAlgorithmSource,
                    AzureKeyVaultSigningAlgorithms
                >()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Azure Key Vault <see cref="IRemoteKeyProvider"/> and its
        /// <see cref="IRemoteSigningAlgorithmSource"/> as <c>AddAzureKeyVaultRemoteKeyProvider</c> does, but
        /// only when the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section exists. Registering a remote
        /// key provider selects it and validates its options when the host starts, so this method registers it only
        /// when its configuration section exists.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> that may hold the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAzureKeyVaultRemoteKeyProviderIfConfigured(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            return configuration
                .GetSection(AzureKeyVaultRemoteKeyProviderOptions.DefaultSectionName)
                .Exists()
                ? builder.AddAzureKeyVaultRemoteKeyProvider(configuration)
                : builder;
        }
    }
}
