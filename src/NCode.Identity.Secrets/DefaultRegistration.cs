// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register services
/// required for managing identity secrets and cryptographic keys.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the entry point for configuring the Identity Secrets library,
/// which manages cryptographic secret keys used for signing, encryption, and other
/// security operations.
/// </para>
/// </remarks>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers all services required for the Identity Secrets library.
        /// </summary>
        /// <returns>An <see cref="IServiceBuilder{T}"/> for <see cref="SecretsLibrary"/> that can be used
        /// to further configure secrets-related services, such as adding persistence support.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the following services:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Data protection services via <c>AddDataProtection()</c>.</description></item>
        /// <item><description><see cref="IDataProtectorFactory{T}"/> - Factory for creating data protectors.</description></item>
        /// <item><description><see cref="IDataProtectorFactory{T}"/> for <see cref="SecretKey"/> - Uses
        /// <see cref="DefaultSecretKeyDataProtectorFactory"/> with ephemeral, process-local keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionProvider"/> - Provides access to collections of secret keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionProviderFactory"/> - Factory for creating secret key collection providers.</description></item>
        /// <item><description><see cref="ISecretKeyFactory"/> - Factory for creating individual secret keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionFactory"/> - Factory for creating secret key collections.</description></item>
        /// <item><description><see cref="IRemoteKeyDescriptorCatalog"/> - Lookup over the described remote key providers and signing algorithm rows.</description></item>
        /// <item><description><see cref="IRemoteKeyProviderCatalog"/> - Lookup over the registered remote key providers.</description></item>
        /// </list>
        /// <para>
        /// Services are registered using <c>TryAddSingleton</c> semantics where applicable,
        /// meaning existing registrations will not be overwritten.
        /// </para>
        /// </remarks>
        public IServiceBuilder<SecretsLibrary> AddSecretsLibrary()
        {
            var builder = serviceCollection.NewBuilder<SecretsLibrary>();

            serviceCollection.AddCollectionProviders();

            // The key-type descriptors log through ILogger<T>, so the library registers logging itself and composes
            // without the host having registered it first; AddLogging is idempotent.
            serviceCollection.AddLogging();

            serviceCollection.AddDataProtection();
            serviceCollection.AddDataProtectorFactory();
            serviceCollection.AddDataProtectorFactory<
                SecretKey,
                DefaultSecretKeyDataProtectorFactory
            >();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProvider,
                DefaultSecretKeyCollectionProvider
            >();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProviderFactory,
                DefaultSecretKeyCollectionProviderFactory
            >();

            serviceCollection.TryAddSingleton<ISecretKeyFactory, DefaultSecretKeyFactory>();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionFactory,
                DefaultSecretKeyCollectionFactory
            >();

            // The secret key-type catalog is the single source of truth for the key-type taxonomy and
            // capabilities (ADR-1067); applications can register additional descriptors to support more types.
            serviceCollection.TryAddSingleton<ISecretKeyTypeCatalog, DefaultSecretKeyTypeCatalog>();

            // The elliptic-curve catalog is the single source of truth for supported curves (ADR-1068);
            // applications can register additional EccCurveSpecification services to support more curves.
            serviceCollection.TryAddSingleton<IEccCurveCatalog, DefaultEccCurveCatalog>();

            // The remote key descriptor catalog holds the provider descriptors and the rows of the
            // IRemoteKeyDescriptorSource services, and the remote key provider catalog holds the registered
            // IRemoteKeyProvider services (ADR-1471, ADR-1474).
            serviceCollection.TryAddSingleton<
                IRemoteKeyDescriptorCatalog,
                DefaultRemoteKeyDescriptorCatalog
            >();
            serviceCollection.TryAddSingleton<
                IRemoteKeyProviderCatalog,
                DefaultRemoteKeyProviderCatalog
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ISecretKeyTypeDescriptor,
                    SymmetricSecretKeyTypeDescriptor
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, RsaSecretKeyTypeDescriptor>()
            );
            // The AKP (Algorithm Key Pair) descriptor backs post-quantum ML-DSA (FIPS 204 / RFC 9964) keys.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, AkpSecretKeyTypeDescriptor>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, EccSecretKeyTypeDescriptor>()
            );

            return builder;
        }
    }
}
