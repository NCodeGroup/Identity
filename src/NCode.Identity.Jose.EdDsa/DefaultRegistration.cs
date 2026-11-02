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
/// Provides extension methods for <see cref="IServiceCollection"/> to register clean-room <c>EdDSA</c> (Ed25519 /
/// Ed448, RFC 8037, RFC 9864) signing — the <c>OKP</c> secret-key type and the <c>EdDSA</c>, <c>Ed25519</c> and
/// <c>Ed448</c> <c>JWS</c> signature algorithms.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers clean-room <c>EdDSA</c> support: the <c>OKP</c> secret-key type descriptor (generate / import /
        /// export / deserialize) and the <c>EdDSA</c>, <c>Ed25519</c> and <c>Ed448</c> <see cref="SignatureAlgorithm"/>
        /// instances (RFC 8037, RFC 9864). Safe to call more than once.
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

            return serviceCollection;
        }
    }
}
