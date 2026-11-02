// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Algorithms;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.EdDsa;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register clean-room <c>OKP</c> (RFC 8037) support
/// — the <c>OKP</c> secret-key type, <c>EdDSA</c> signing over Ed25519 and Ed448 (the <c>EdDSA</c>, <c>Ed25519</c> and
/// <c>Ed448</c> <c>JWS</c> signature algorithms, RFC 9864), and <c>ECDH-ES</c> key agreement over X25519 and X448.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers clean-room <c>OKP</c> support: the <c>OKP</c> secret-key type descriptor (generate / import /
        /// export / deserialize) for the Ed25519, Ed448, X25519 and X448 curves, the <c>EdDSA</c>, <c>Ed25519</c> and
        /// <c>Ed448</c> <see cref="SignatureAlgorithm"/> instances (RFC 8037, RFC 9864), and the X25519 and X448
        /// <see cref="IEcdhKeyAgreement"/>, through which the <c>ECDH-ES</c>, <c>ECDH-ES+A128KW</c>,
        /// <c>ECDH-ES+A192KW</c> and <c>ECDH-ES+A256KW</c> key-management algorithms accept <c>OKP</c> keys
        /// (RFC 8037 §3.2). Safe to call more than once.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddEdDsa()
        {
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, OkpSecretKeyTypeDescriptor>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<Algorithm>,
                    EdDsaAlgorithmDataSource
                >()
            );

            // See ADR-1390.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEcdhKeyAgreement, OkpEcdhKeyAgreement>()
            );

            return serviceCollection;
        }
    }
}
