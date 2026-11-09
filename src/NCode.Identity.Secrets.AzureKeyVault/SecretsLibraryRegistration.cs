// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets.AzureKeyVault;

/// <summary>
/// Provides opt-in extension methods that register the Azure Key Vault remote key provider on the secrets-library
/// builder, beside the key wrapper <c>DefaultRegistration</c> registers on the secret-persistence-library builder
/// (ADR-1069).
/// </summary>
[PublicAPI]
public static class SecretsLibraryRegistration
{
    /// <param name="builder">The secrets-library builder returned by <c>AddSecretsLibrary</c>.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Registers the Azure Key Vault <see cref="IRemoteKeyProvider"/>, which holds the keys of tenants and of
        /// client key rings, creates tenant keys in Azure Key Vault or Managed HSM that never leave the service,
        /// signs, decrypts and agrees with them, and disables them, together with the
        /// <see cref="IRemoteKeyDescriptorSource"/> that describes the provider and maps each JOSE algorithm to the
        /// key type and algorithm of the service. The
        /// <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section binds
        /// <see cref="AzureKeyVaultRemoteKeyProviderOptions"/> strictly and is validated when the host starts, so
        /// registering the provider selects it. No client is created before the first key is created, signed with,
        /// decrypted with or destroyed. Credentials come from <see cref="Azure.Identity.DefaultAzureCredential"/>,
        /// never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretsLibrary> AddAzureKeyVaultRemoteKeyProvider(
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
                    IRemoteKeyDescriptorSource,
                    AzureKeyVaultRemoteKeyDescriptors
                >()
            );

            return builder;
        }

        /// <summary>
        /// Registers the Azure Key Vault <see cref="IRemoteKeyProvider"/> and its
        /// <see cref="IRemoteKeyDescriptorSource"/> as <c>AddAzureKeyVaultRemoteKeyProvider</c> does, but
        /// only when the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section exists. Registering a remote
        /// key provider selects it and validates its options when the host starts, so this method registers it only
        /// when its configuration section exists.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> that may hold the <c>SecretProtection:RemoteKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretsLibrary> AddAzureKeyVaultRemoteKeyProviderIfConfigured(
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
