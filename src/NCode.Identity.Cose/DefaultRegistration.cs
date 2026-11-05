// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cose.Options;
using NCode.Identity.Cose.Randomness;
using NCode.Registration;

namespace NCode.Identity.Cose;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{TMarker}"/> to register CBOR Object Signing and
/// Encryption (COSE, RFC 9052 / 9053): the serializer, the algorithms, the key types and curves, and the COSE_Key
/// converter.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// The algorithms that <c>AddCoseLibrary</c> registers. Those that need weaker primitives or serve interoperability
    /// only, HMAC 256/64, RSAES-OAEP with SHA-1 and RSASSA-PKCS1-v1_5, are offered for an application to register.
    /// </summary>
    private static readonly CoseAlgorithm[] DefaultAlgorithms =
    [
        CoseAlgorithms.Es256,
        CoseAlgorithms.Es384,
        CoseAlgorithms.Es512,
        CoseAlgorithms.Esp256,
        CoseAlgorithms.Esp384,
        CoseAlgorithms.Esp512,
        CoseAlgorithms.Es256K,
        CoseAlgorithms.EdDsa,
        CoseAlgorithms.Ed25519,
        CoseAlgorithms.Ed448,
        CoseAlgorithms.Ps256,
        CoseAlgorithms.Ps384,
        CoseAlgorithms.Ps512,
        CoseAlgorithms.MlDsa44,
        CoseAlgorithms.MlDsa65,
        CoseAlgorithms.MlDsa87,
        CoseAlgorithms.HmacSha256,
        CoseAlgorithms.HmacSha384,
        CoseAlgorithms.HmacSha512,
        CoseAlgorithms.A128Gcm,
        CoseAlgorithms.A192Gcm,
        CoseAlgorithms.A256Gcm,
        CoseAlgorithms.Direct,
        CoseAlgorithms.A128Kw,
        CoseAlgorithms.A192Kw,
        CoseAlgorithms.A256Kw,
        CoseAlgorithms.RsaesOaepSha256,
        CoseAlgorithms.RsaesOaepSha512,
    ];

    private static readonly CoseKeyTypeDescriptor[] DefaultKeyTypes =
    [
        CoseKeyTypeDescriptors.Okp,
        CoseKeyTypeDescriptors.Ec2,
        CoseKeyTypeDescriptors.Rsa,
        CoseKeyTypeDescriptors.Symmetric,
        CoseKeyTypeDescriptors.Akp,
    ];

    private static readonly CoseEllipticCurve[] DefaultCurves =
    [
        CoseEllipticCurves.P256,
        CoseEllipticCurves.P384,
        CoseEllipticCurves.P521,
        CoseEllipticCurves.X25519,
        CoseEllipticCurves.X448,
        CoseEllipticCurves.Ed25519,
        CoseEllipticCurves.Ed448,
        CoseEllipticCurves.Secp256k1,
    ];

    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="Jose.JoseLibrary"/>, whose
    /// algorithms and secret keys serve COSE.</param>
    extension(IServiceBuilder<Jose.JoseLibrary> builder)
    {
        /// <summary>
        /// Registers the COSE services with the default <see cref="CoseOptions"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> of the <see cref="CoseLibrary"/>.</returns>
        public IServiceBuilder<CoseLibrary> AddCoseLibrary() =>
            builder.AddCoseLibrary(static _ => { });

        /// <summary>
        /// Registers the COSE services: <see cref="ICoseSerializer"/>, <see cref="ICoseKeyConverter"/>, the default
        /// algorithms, key types and curves, and their catalogs, with the <see cref="CoseOptions"/> the callback
        /// configures, validated when the host starts.
        /// </summary>
        /// <param name="configureOptions">The callback that configures the <see cref="CoseOptions"/>.</param>
        /// <returns>The <see cref="IServiceBuilder{T}"/> of the <see cref="CoseLibrary"/>.</returns>
        public IServiceBuilder<CoseLibrary> AddCoseLibrary(Action<CoseOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var serviceCollection = builder.ServiceCollection;
            var newBuilder = builder.NewBuilder<CoseLibrary>();

            serviceCollection
                .AddOptions<CoseOptions>()
                .Configure(configureOptions)
                .ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<CoseOptions>, CoseOptionsValidator>()
            );

            // The algorithms, key types and curves are data, registered as instances beside any the application
            // registers.
            foreach (var algorithm in DefaultAlgorithms)
                serviceCollection.TryAddEnumerableInstance(algorithm);

            foreach (var keyType in DefaultKeyTypes)
                serviceCollection.TryAddEnumerableInstance(keyType);

            foreach (var curve in DefaultCurves)
                serviceCollection.TryAddEnumerableInstance(curve);

            serviceCollection.TryAddSingleton<ICoseAlgorithmCatalog, DefaultCoseAlgorithmCatalog>();
            serviceCollection.TryAddSingleton<ICoseKeyTypeCatalog, DefaultCoseKeyTypeCatalog>();
            serviceCollection.TryAddSingleton<ICoseKeyConverter, DefaultCoseKeyConverter>();
            serviceCollection.TryAddSingleton<ICoseRandomSource, DefaultCoseRandomSource>();
            serviceCollection.TryAddSingleton<ICoseSerializer, DefaultCoseSerializer>();
            serviceCollection.TryAddSingleton(TimeProvider.System);

            return newBuilder;
        }
    }
}
