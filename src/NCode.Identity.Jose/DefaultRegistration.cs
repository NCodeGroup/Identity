#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Secrets;
using NCode.Registration;

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

            serviceCollection.TryAddSingleton<IJoseSerializer, JoseSerializer>();

            return newBuilder;
        }
    }
}
