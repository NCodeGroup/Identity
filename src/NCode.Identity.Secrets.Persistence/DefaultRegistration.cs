// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.Verifiers;
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
        /// <item><description><see cref="ISecretVerifierSchemeCatalog"/> - Looks up the registered
        /// <see cref="ISecretVerifierScheme"/> instances, which the <see cref="SecretVerifierSchemes.Sha256"/> and
        /// <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/> schemes join as a collection.</description></item>
        /// <item><description><see cref="ISecretVerificationGate"/> - Bounds the verifications with a work factor that
        /// run and wait at once, whatever their scheme.</description></item>
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
        /// <item><description><see cref="IMasterKeyManager"/> - Rotates master keys, rewraps the values that retired
        /// keys protect and destroys retired keys that nothing names.</description></item>
        /// <item><description>Options for <see cref="SecretProtectionOptions"/> and
        /// <see cref="LocalKeyWrapperOptions"/>, which are bound to configuration elsewhere, and for
        /// <see cref="SharedSecretVerifierOptions"/>, <see cref="Pbkdf2SecretVerifierOptions"/> and
        /// <see cref="SecretVerificationGateOptions"/>, which are validated as the host starts.</description></item>
        /// </list>
        /// <para>
        /// Services are registered using <c>TryAdd*</c> semantics, meaning existing registrations
        /// will not be overwritten. The <see cref="IKeyWrapper"/> and <see cref="ISecretVerifierScheme"/>
        /// implementations are added as collections.
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

            // Shared secret verifiers: a package adds a scheme by registering another ISecretVerifierScheme, and a scheme
            // with a work factor takes its turns from the one ISecretVerificationGate. See ADR-1391.
            serviceCollection.TryAddSingleton<
                ISecretVerifierSchemeCatalog,
                DefaultSecretVerifierSchemeCatalog
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretVerifierScheme, Sha256SecretVerifierScheme>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretVerifierScheme, Pbkdf2SecretVerifierScheme>()
            );
            serviceCollection.TryAddSingleton<
                ISecretVerificationGate,
                DefaultSecretVerificationGate
            >();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<SharedSecretVerifierOptions>,
                    SharedSecretVerifierOptionsValidator
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<Pbkdf2SecretVerifierOptions>,
                    Pbkdf2SecretVerifierOptionsValidator
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<SecretVerificationGateOptions>,
                    SecretVerificationGateOptionsValidator
                >()
            );

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
            serviceCollection.TryAddSingleton<IMasterKeyManager, DefaultMasterKeyManager>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<SecretProtectionOptions>,
                    MasterKeyOptionsValidator
                >()
            );
            serviceCollection.AddOptions<SecretProtectionOptions>();
            serviceCollection.AddOptions<LocalKeyWrapperOptions>();
            serviceCollection.AddOptions<SharedSecretVerifierOptions>().ValidateOnStart();
            serviceCollection.AddOptions<Pbkdf2SecretVerifierOptions>().ValidateOnStart();
            serviceCollection.AddOptions<SecretVerificationGateOptions>().ValidateOnStart();

            return newBuilder;
        }
    }
}
