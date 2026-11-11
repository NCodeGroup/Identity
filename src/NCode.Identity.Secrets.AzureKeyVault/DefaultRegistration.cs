// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;

namespace NCode.Identity.Secrets.AzureKeyVault;

/// <summary>
/// Provides opt-in extension methods that register the Azure Key Vault key wrapper on the secret-persistence-library
/// builder.
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
    }
}
