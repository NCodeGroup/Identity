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
using NCode.Identity.Secrets.Persistence.AzureKeyVault.Facades;
using NCode.Identity.Secrets.Persistence.AzureKeyVault.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SecretPersistenceLibrary"/> builder to register the Azure Key
/// Vault root key provider.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The secret-persistence-library builder returned by <c>AddPersistenceLibrary</c>.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the Azure Key Vault <see cref="IRootKeyProvider"/>, which wraps master keys with an RSA key in
        /// Azure Key Vault or Managed HSM that never leaves the service. The
        /// <c>SecretProtection:RootKeyProviders:AzureKeyVault</c> section binds
        /// <see cref="AzureKeyVaultRootKeyOptions"/> strictly. The provider stays inert until the
        /// <c>SecretProtection:RootKeyProvider</c> setting selects <c>azure-key-vault</c>, and no client is created
        /// before the first key is wrapped or unwrapped. Credentials come from
        /// <see cref="Azure.Identity.DefaultAzureCredential"/>, never from configuration.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> holding the <c>SecretProtection:RootKeyProviders:AzureKeyVault</c> section.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddAzureKeyVaultRootKeyProvider(
            IConfiguration configuration
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<AzureKeyVaultRootKeyOptions>()
                .Configure(options =>
                    configuration
                        .GetSection(AzureKeyVaultRootKeyOptions.DefaultSectionName)
                        .BindStrictly(options)
                )
                .ValidateOnStart();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AzureKeyVaultRootKeyOptions>,
                    AzureKeyVaultRootKeyOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton<
                IAzureKeyVaultClientFactory,
                DefaultAzureKeyVaultClientFactory
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRootKeyProvider, AzureKeyVaultRootKeyProvider>()
            );

            return builder;
        }
    }
}
