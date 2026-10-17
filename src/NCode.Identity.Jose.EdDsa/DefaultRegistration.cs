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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Algorithms;
using NCode.Identity.Jose.EdDsa.Logic;
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.Jose.EdDsa;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register clean-room <c>EdDSA</c> (Ed25519 /
/// Ed448, RFC 8037) signing — the <c>OKP</c> secret-key type and the <c>EdDSA</c> <c>JWS</c> signature algorithm.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers clean-room <c>EdDSA</c> support: the <c>OKP</c> secret-key type descriptor (generate / import /
        /// export / deserialize) and the <c>EdDSA</c> <see cref="SignatureAlgorithm"/>. Safe to call more than once.
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
