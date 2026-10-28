#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register services
/// required for persisting and serializing identity secrets.
/// </summary>
/// <remarks>
/// <para>
/// This class extends the <see cref="SecretsLibrary"/> service builder to add persistence capabilities,
/// including secret serialization for storage in databases or other persistent stores.
/// </para>
/// </remarks>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> instance for configuring
    /// <see cref="SecretsLibrary"/> services.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Adds services required for persisting identity secrets, including serialization support.
        /// </summary>
        /// <returns>A new <see cref="IServiceBuilder{T}"/> for <see cref="SecretPersistenceLibrary"/>
        /// that can be used to further configure persistence-related services.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the following services:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="ISecretSerializer"/> - Serializes and deserializes secret keys
        /// for persistent storage.</description></item>
        /// <item><description><see cref="ISecretGenerator"/> - Generates secret key material server-side,
        /// protected under the owner's master key.</description></item>
        /// <item><description><see cref="ISecretImporter"/> - Imports externally supplied secret key
        /// material.</description></item>
        /// <item><description><see cref="TimeProvider"/> - Supplies the current time, defaulting to
        /// <see cref="TimeProvider.System"/>.</description></item>
        /// <item><description><see cref="IKeyWrapper"/> - The ephemeral and local root key providers,
        /// which wrap master keys with a root key.</description></item>
        /// <item><description>A catalog that selects the configured root key provider.</description></item>
        /// <item><description>The recovery key wrapper, which wraps master keys with the configured
        /// recovery public key.</description></item>
        /// <item><description>The factory that creates and unwraps master keys.</description></item>
        /// <item><description><see cref="ISecretProtectorProvider"/> - Provides secret protectors backed by
        /// the cached, persisted master key ring.</description></item>
        /// <item><description>Options for <see cref="SecretProtectionOptions"/> and
        /// <see cref="LocalKeyWrapperOptions"/>, which are bound to configuration elsewhere.</description></item>
        /// </list>
        /// <para>
        /// Services are registered using <c>TryAdd*</c> semantics, meaning existing registrations
        /// will not be overwritten. The <see cref="IKeyWrapper"/> implementations are added as a
        /// collection.
        /// </para>
        /// </remarks>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddPersistenceLibrary()
        {
            var newBuilder = builder.NewBuilder<SecretPersistenceLibrary>();

            var serviceCollection = builder.ServiceCollection;
            serviceCollection.TryAddSingleton<ISecretSerializer, DefaultSecretSerializer>();
            serviceCollection.TryAddSingleton<ISecretGenerator, DefaultSecretGenerator>();
            serviceCollection.TryAddSingleton<ISecretImporter, DefaultSecretImporter>();

            // Secret protection
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IKeyWrapper, EphemeralKeyWrapper>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IKeyWrapper, LocalKeyWrapper>()
            );
            serviceCollection.TryAddSingleton<IKeyWrapperCatalog, DefaultKeyWrapperCatalog>();
            serviceCollection.TryAddSingleton<IRecoveryKeyWrapper, RecoveryKeyWrapper>();
            serviceCollection.TryAddSingleton<MasterKeyFactory>();
            serviceCollection.TryAddSingleton<ISecretProtectorProvider, DefaultMasterKeyRing>();
            serviceCollection.AddOptions<SecretProtectionOptions>();
            serviceCollection.AddOptions<LocalKeyWrapperOptions>();

            return newBuilder;
        }
    }
}
