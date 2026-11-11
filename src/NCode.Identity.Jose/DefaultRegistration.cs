// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Registration;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the required services needed for using
/// Jose algorithms.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="SecretsLibrary"/>.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Registers the required Jose services and algorithms, layered on the <see cref="SecretsLibrary"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JoseLibrary> AddJoseLibrary()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = builder.NewBuilder<JoseLibrary>();

            serviceCollection.TryAddSingleton<IAesKeyWrap, DefaultAesKeyWrap>();

            serviceCollection.TryAddSingleton<
                IAlgorithmCollectionProvider,
                DefaultAlgorithmCollectionProvider
            >();

            serviceCollection.TryAddSingleton<ICredentialSelector, DefaultCredentialSelector>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<Algorithm>,
                    DefaultAlgorithmDataSource
                >()
            );

            // The ECDH-ES algorithms serve every key type that has a registered agreement (ADR-1389).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEcdhKeyAgreement, EccEcdhKeyAgreement>()
            );

            // ES256, ES384 and ES512 (RFC 7518 §3.4) are registered as data, as a package registers ES256K (ADR-1392).
            serviceCollection.TryAddEnumerableInstance(EccSignatureAlgorithmSpecification.Es256);
            serviceCollection.TryAddEnumerableInstance(EccSignatureAlgorithmSpecification.Es384);
            serviceCollection.TryAddEnumerableInstance(EccSignatureAlgorithmSpecification.Es512);

            serviceCollection.TryAddSingleton<IJoseSerializer, JoseSerializer>();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<IDPoPProofService, DefaultDPoPProofService>();
            serviceCollection.TryAddSingleton<IRemoteKeyProver, DefaultRemoteKeyProver>();

            return newBuilder;
        }
    }
}
