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
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the required Jose services and algorithms to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddJoseServices() =>
            serviceCollection.AddJoseServices(_ => { });

        /// <summary>
        /// Registers the required Jose services and algorithms to the specified <see cref="IServiceCollection"/> instance.
        /// </summary>
        /// <param name="configureJoseOptions">The action used to configure the <see cref="JoseSerializerOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddJoseServices(Action<JoseSerializerOptions> configureJoseOptions)
        {
            serviceCollection.VerifyIsRegistered<SecretsLibrary>();

            serviceCollection.AddRegistrationMarker<JoseLibrary>();

            serviceCollection.Configure(configureJoseOptions);

            serviceCollection.TryAddSingleton<
                IAesKeyWrap,
                DefaultAesKeyWrap
            >();

            serviceCollection.TryAddSingleton<
                IAlgorithmCollectionProvider,
                DefaultAlgorithmCollectionProvider
            >();

            serviceCollection.TryAddSingleton<
                ICredentialSelector,
                DefaultCredentialSelector
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<Algorithm>,
                    DefaultAlgorithmDataSource
                >()
            );

            serviceCollection.TryAddSingleton<
                IJoseSerializer,
                JoseSerializer
            >();

            return serviceCollection;
        }
    }
}
